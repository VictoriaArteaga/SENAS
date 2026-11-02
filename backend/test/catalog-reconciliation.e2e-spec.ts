import { INestApplication } from '@nestjs/common';
import { DataSource } from 'typeorm';
import { DatabaseBootstrapService } from '../src/database/database-bootstrap.service';
import { CatalogSeeder } from '../src/database/seeds/catalog.seeder';
import { createApp, registerUser } from './helpers/app';

describe('Catalog seed reconciliation (e2e)', () => {
  let app: INestApplication;
  let ds: DataSource;
  let seeder: CatalogSeeder;

  const reseed = () => ds.transaction((em) => seeder.seed(em));
  const q = <T = Record<string, unknown>>(
    sql: string,
    params: unknown[] = [],
  ) => ds.query(sql, params) as Promise<T[]>;
  const codes = async (level: string) =>
    (
      await q<{ code: string }>(
        `select s.code from level_signs ls
           join signs s on s.id = ls.sign_id
           join levels l on l.id = ls.level_id
          where l.code = $1 order by ls.position`,
        [level],
      )
    ).map((r) => r.code);
  const activeSigns = async () =>
    (
      await q<{ n: number }>(`select count(*)::int n from signs where active`)
    )[0].n;

  beforeAll(async () => {
    app = await createApp();
    ds = app.get(DataSource);
    seeder = app.get(CatalogSeeder);
    // Other e2e files share this database; their history would make renamed signs
    // "referenced" (and therefore deactivated instead of deleted).
    await q('delete from evaluations');
    await q('delete from sign_progress');
  });
  afterAll(async () => {
    // Leave the shared test database exactly as the seed defines it.
    await q(
      `delete from sign_progress where sign_id in (select id from signs where code like 'OLD\\_%')`,
    );
    await q(`delete from signs where code like 'OLD\\_%'`);
    await app.close();
  });

  it('starts with the official catalog: Unity ids for letters and numbers, backend codes for the rest', async () => {
    const alphabet = await codes('ALPHABET');
    expect(alphabet).toHaveLength(27);
    expect(alphabet.slice(0, 3)).toEqual(['A', 'B', 'C']);
    expect(alphabet[14]).toBe('Ñ');
    expect(alphabet[26]).toBe('Z');
    const numbers = await codes('NUMBERS');
    expect(numbers[0]).toBe('numeros.0');
    expect(numbers[30]).toBe('numeros.30');
    expect((await codes('MONTHS'))[0]).toBe('MONTH_JANUARY');
    expect((await codes('GREETINGS_PHRASES'))[3]).toBe('WORD_HELLO');
    expect(await activeSigns()).toBe(81);
  });

  it('a changed code does not duplicate: the old sign is replaced', async () => {
    // Simulates the database as it was before the codes changed.
    await q(`update signs set code = 'LETTER_A' where code = 'A'`);
    await q(`update signs set code = 'NUMBER_0' where code = 'numeros.0'`);

    const report = await reseed();

    expect(report.deleted.sort()).toEqual(['LETTER_A', 'NUMBER_0']);
    expect(report.deactivated).toEqual([]);
    expect(
      await q(`select 1 from signs where code in ('LETTER_A','NUMBER_0')`),
    ).toHaveLength(0);
    expect((await codes('ALPHABET'))[0]).toBe('A');
    expect((await codes('NUMBERS'))[0]).toBe('numeros.0');
    expect(await activeSigns()).toBe(81);
    expect((await q(`select count(*)::int n from signs`))[0]).toEqual({
      n: 81,
    });
  });

  it('reordering does not trip the unique (level_id, position) constraint', async () => {
    // Swap A and B in the database.
    await q(`update level_signs set position = position + 1000
              where sign_id in (select id from signs where code in ('A','B'))`);
    await q(`update level_signs set position = 2
              where sign_id = (select id from signs where code = 'A')`);
    await q(`update level_signs set position = 1
              where sign_id = (select id from signs where code = 'B')`);
    expect((await codes('ALPHABET')).slice(0, 2)).toEqual(['B', 'A']);

    await reseed();

    expect((await codes('ALPHABET')).slice(0, 3)).toEqual(['A', 'B', 'C']);
  });

  it('an orphan sign nothing references is deleted, with its link', async () => {
    await q(
      `insert into signs (code, name, type, active) values ('OLD_UNUSED', 'Old', 'LETTER', true)`,
    );
    await q(`insert into level_signs (level_id, sign_id, position)
             select l.id, s.id, 99 from levels l, signs s where l.code = 'ALPHABET' and s.code = 'OLD_UNUSED'`);
    expect(await activeSigns()).toBe(82);

    const report = await reseed();

    expect(report.deleted).toContain('OLD_UNUSED');
    expect(
      await q(`select 1 from signs where code = 'OLD_UNUSED'`),
    ).toHaveLength(0);
    expect(await codes('ALPHABET')).toHaveLength(27);
    expect(await activeSigns()).toBe(81);
  });

  it('an orphan sign that history references is deactivated, not deleted, and unlinked', async () => {
    const user = await registerUser(app, 'Reconcile');
    await q(
      `insert into signs (code, name, type, active) values ('OLD_USED', 'Old', 'LETTER', true)`,
    );
    await q(`insert into level_signs (level_id, sign_id, position)
             select l.id, s.id, 98 from levels l, signs s where l.code = 'ALPHABET' and s.code = 'OLD_USED'`);
    await q(
      `insert into sign_progress (user_id, sign_id) select $1, id from signs where code = 'OLD_USED'`,
      [user.userId],
    );

    const report = await reseed();

    expect(report.deactivated).toContain('OLD_USED');
    expect(report.deleted).not.toContain('OLD_USED');
    const [row] = await q<{ active: boolean }>(
      `select active from signs where code = 'OLD_USED'`,
    );
    expect(row.active).toBe(false);
    expect(
      await q(
        `select 1 from level_signs ls join signs s on s.id = ls.sign_id where s.code = 'OLD_USED'`,
      ),
    ).toHaveLength(0);
    expect(await codes('ALPHABET')).toHaveLength(27);
    // The player's progress row survives.
    expect(
      await q(`select 1 from sign_progress where user_id = $1`, [user.userId]),
    ).toHaveLength(1);
    expect(await activeSigns()).toBe(81);
  });

  it('reseeding is idempotent', async () => {
    const snapshot = async () =>
      JSON.stringify(
        await q(`select l.code l, s.code s, ls.position p from level_signs ls
                   join levels l on l.id = ls.level_id join signs s on s.id = ls.sign_id
                  where s.active order by l.position, ls.position`),
      );
    const before = await snapshot();
    const report = await reseed();
    expect(report.deleted).toEqual([]);
    expect(await snapshot()).toBe(before);
  });

  it('verify fails when the active catalog is not 4 levels / 81 signs', async () => {
    await ds
      .transaction(async (em) => {
        await seeder.seed(em);
        expect(await seeder.verify(em)).toBe(true);
        await em.query(`update signs set active = false where code = 'B'`);
        expect(await seeder.verify(em)).toBe(false);
        throw new Error('rollback');
      })
      .catch((e: Error) => expect(e.message).toBe('rollback'));
    expect(await activeSigns()).toBe(81);
  });

  it('if verification fails at startup the transaction is rolled back and the app does not start', async () => {
    await q(
      `insert into signs (code, name, type, active) values ('OLD_LEFTOVER', 'Old', 'LETTER', true)`,
    );
    jest.spyOn(seeder, 'verify').mockResolvedValueOnce(false);

    await expect(
      app.get(DatabaseBootstrapService).onApplicationBootstrap(),
    ).rejects.toThrow(/rolled back/);

    // The seed's cleanup of the orphan was rolled back with the rest.
    expect(
      await q(`select 1 from signs where code = 'OLD_LEFTOVER'`),
    ).toHaveLength(1);
    await reseed();
    expect(
      await q(`select 1 from signs where code = 'OLD_LEFTOVER'`),
    ).toHaveLength(0);
  });
});
