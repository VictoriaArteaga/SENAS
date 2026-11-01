import {
  evaluateLevelAccess,
  findNumberingInconsistencies,
  PlayerState,
  RuleLevel,
  SenaProgress,
  sortByProgression,
} from './level-access.rules';

const L = (id: string, numero: number, orden: number, xpRequerida: number, precisionMinima: number): RuleLevel => ({
  id, numero, orden, xpRequerida, precisionMinima,
});
// L1 (xp 0, min 60) -> L2 (xp 100, min 75) -> L3 (xp 250, min 80) -> L4 (xp 400, min 90)
const L1 = L('L1', 1, 10, 0, 60);
const L2 = L('L2', 2, 20, 100, 75);
const L3 = L('L3', 3, 30, 250, 80);
const L4 = L('L4', 4, 40, 400, 90);
const LEVELS = [L1, L2, L3, L4];

// Señas obligatorias: L1 {s1, s2}; L2 {s4}; L3 ninguna.
const MANDATORY = new Map<string, string[]>([['L1', ['s1', 's2']], ['L2', ['s4']]]);

const player = (over: Partial<{ totalXp: number; nivelActual: number; progress: Record<string, SenaProgress> }> = {}): PlayerState => ({
  totalXp: over.totalXp ?? 0,
  nivelActual: over.nivelActual ?? 1,
  mandatorySenasByLevel: MANDATORY,
  progressBySena: new Map(Object.entries(over.progress ?? {})),
});
const ok = (mejorPrecision: number): SenaProgress => ({ completada: true, mejorPrecision });
const summary = (levels: RuleLevel[], p: PlayerState) =>
  Object.fromEntries(evaluateLevelAccess(levels, p).map((a) => [a.levelId, [a.estado, a.motivosBloqueo]]));

describe('evaluateLevelAccess', () => {
  describe('primer nivel (sin nivel anterior)', () => {
    it('solo exige su propia XP: con xpRequerida 0 un jugador sin nada lo tiene y es ACTUAL', () => {
      expect(summary(LEVELS, player()).L1).toEqual(['ACTUAL', []]);
    });
    it('si el primer nivel exige XP, sin ella queda BLOQUEADO por XP_INSUFICIENTE (aunque sea el nivelActual)', () => {
      const strict = [L('A', 1, 1, 50, 60)];
      expect(summary(strict, player({ totalXp: 49 })).A).toEqual(['BLOQUEADO', ['XP_INSUFICIENTE']]);
      expect(summary(strict, player({ totalXp: 50 })).A).toEqual(['ACTUAL', []]);
    });
    it('nunca produce motivos de señas ni de nivel anterior', () => {
      expect(evaluateLevelAccess([L('A', 1, 1, 999, 99)], player())[0].motivosBloqueo).toEqual(['XP_INSUFICIENTE']);
    });
  });

  describe('condición de XP (la del nivel que se abre)', () => {
    const full = { s1: ok(90), s2: ok(90), s4: ok(90) };
    it('totalXp igual a xpRequerida cumple (>=); un punto menos no', () => {
      expect(summary(LEVELS, player({ totalXp: 100, progress: full })).L2[1]).toEqual([]);
      expect(summary(LEVELS, player({ totalXp: 99, progress: full })).L2[1]).toEqual(['XP_INSUFICIENTE']);
    });
    it('la XP exigida es la del nivel destino, no la del anterior', () => {
      // 250 XP bastan para L3 (xp 250) pero L2 exige 100 y su propio L2 cumple; L3 se abre con L2 completo.
      const s = summary(LEVELS, player({ totalXp: 250, progress: full }));
      expect(s.L3[1]).toEqual([]);
      expect(s.L4[1]).toEqual(['XP_INSUFICIENTE']); // L4 exige 400
    });
  });

  describe('señas obligatorias del nivel anterior', () => {
    const xp = 100;
    it('sin registro de progreso en ninguna: SENAS_OBLIGATORIAS_PENDIENTES', () => {
      expect(summary(LEVELS, player({ totalXp: xp })).L2[1]).toEqual(['SENAS_OBLIGATORIAS_PENDIENTES']);
    });
    it('una seña sin registro basta para bloquear aunque la otra esté perfecta', () => {
      expect(summary(LEVELS, player({ totalXp: xp, progress: { s1: ok(100) } })).L2[1]).toEqual(['SENAS_OBLIGATORIAS_PENDIENTES']);
    });
    it('registro con completada=false cuenta como pendiente', () => {
      const p = player({ totalXp: xp, progress: { s1: ok(90), s2: { completada: false, mejorPrecision: 90 } } });
      expect(summary(LEVELS, p).L2[1]).toEqual(['SENAS_OBLIGATORIAS_PENDIENTES']);
    });
    it('una seña sin completar y con mejor precisión bajo el umbral incumple ambas condiciones', () => {
      const p = player({ totalXp: xp, progress: { s1: ok(90), s2: { completada: false, mejorPrecision: 50 } } });
      expect(summary(LEVELS, p).L2[1]).toEqual(['SENAS_OBLIGATORIAS_PENDIENTES', 'PRECISION_INSUFICIENTE']);
    });
    it('las señas NO obligatorias del nivel anterior se ignoran (practicadas o no)', () => {
      // s3 no está en la lista de obligatorias de L1: tenerla perfecta no sustituye a s1 y s2.
      expect(summary(LEVELS, player({ totalXp: xp, progress: { s3: ok(100) } })).L2[1]).toEqual(['SENAS_OBLIGATORIAS_PENDIENTES']);
      // y no tenerla no estorba.
      expect(summary(LEVELS, player({ totalXp: xp, progress: { s1: ok(80), s2: ok(80) } })).L2[1]).toEqual([]);
    });
    it('si el nivel anterior no tiene señas obligatorias solo cuentan XP y la progresión', () => {
      // L3 no tiene obligatorias: abrir L4 depende de XP (400) y de que L3 esté abierto.
      const p = player({ totalXp: 400, progress: { s1: ok(90), s2: ok(90), s4: ok(90) } });
      expect(summary(LEVELS, p).L4).toEqual(['DESBLOQUEADO', []]);
    });
    it('una seña repetida en la lista de obligatorias no altera el resultado', () => {
      const p: PlayerState = { ...player({ totalXp: xp, progress: { s1: ok(90), s2: ok(90) } }), mandatorySenasByLevel: new Map([['L1', ['s1', 's1', 's2']]]) };
      expect(summary(LEVELS, p).L2[1]).toEqual([]);
    });
  });

  describe('precisión por seña, contra la precisionMinima del nivel anterior', () => {
    const xp = 100;
    it('exactamente igual al umbral cumple (>=); una centésima menos no', () => {
      expect(summary(LEVELS, player({ totalXp: xp, progress: { s1: ok(60), s2: ok(60) } })).L2[1]).toEqual([]);
      expect(summary(LEVELS, player({ totalXp: xp, progress: { s1: ok(60), s2: ok(59.99) } })).L2[1]).toEqual(['PRECISION_INSUFICIENTE']);
    });
    it('no se compensa: 100 % en una seña no cubre una por debajo del umbral', () => {
      expect(summary(LEVELS, player({ totalXp: xp, progress: { s1: ok(100), s2: ok(10) } })).L2[1]).toEqual(['PRECISION_INSUFICIENTE']);
    });
    it('usa la precisionMinima del nivel ANTERIOR (60), no la del nivel que se abre (75)', () => {
      expect(summary(LEVELS, player({ totalXp: xp, progress: { s1: ok(65), s2: ok(65) } })).L2[1]).toEqual([]);
      // Y para abrir L3 se usa la de L2 (75) sobre s4:
      const base = { s1: ok(90), s2: ok(90) };
      expect(summary(LEVELS, player({ totalXp: 250, progress: { ...base, s4: ok(74.99) } })).L3[1]).toEqual(['PRECISION_INSUFICIENTE']);
      expect(summary(LEVELS, player({ totalXp: 250, progress: { ...base, s4: ok(75) } })).L3[1]).toEqual([]);
    });
  });

  describe('estados', () => {
    const all = { s1: ok(90), s2: ok(90), s4: ok(90) };
    it('ACTUAL es el nivel cuyo numero coincide con nivelActual, si está desbloqueado', () => {
      const s = summary(LEVELS, player({ totalXp: 1000, nivelActual: 2, progress: all }));
      expect(s).toEqual({ L1: ['DESBLOQUEADO', []], L2: ['ACTUAL', []], L3: ['DESBLOQUEADO', []], L4: ['DESBLOQUEADO', []] });
    });
    it('exactamente un nivel es ACTUAL como máximo', () => {
      const states = evaluateLevelAccess(LEVELS, player({ totalXp: 1000, nivelActual: 3, progress: all })).map((a) => a.estado);
      expect(states.filter((e) => e === 'ACTUAL')).toHaveLength(1);
    });
    it('si nivelActual apunta a un nivel bloqueado, ese nivel es BLOQUEADO con sus motivos reales y no hay ACTUAL', () => {
      const s = summary(LEVELS, player({ nivelActual: 3 }));
      expect(s.L3[0]).toBe('BLOQUEADO');
      expect(Object.values(s).some(([estado]) => estado === 'ACTUAL')).toBe(false);
    });
    it('si nivelActual no corresponde a ningún nivel no hay ACTUAL y el primero queda DESBLOQUEADO', () => {
      expect(summary(LEVELS, player({ nivelActual: 999 })).L1).toEqual(['DESBLOQUEADO', []]);
    });
    it('desbloqueado es true exactamente cuando no hay motivos', () => {
      for (const a of evaluateLevelAccess(LEVELS, player({ totalXp: 120, progress: { s1: ok(70) } }))) {
        expect(a.desbloqueado).toBe(a.motivosBloqueo.length === 0);
        expect(a.estado === 'BLOQUEADO').toBe(!a.desbloqueado);
      }
    });
  });

  describe('progresión secuencial (sin saltos)', () => {
    it('un jugador sin nada: todos los posteriores bloqueados, cada uno con sus motivos', () => {
      expect(summary(LEVELS, player())).toEqual({
        L1: ['ACTUAL', []],
        L2: ['BLOQUEADO', ['XP_INSUFICIENTE', 'SENAS_OBLIGATORIAS_PENDIENTES']],
        L3: ['BLOQUEADO', ['XP_INSUFICIENTE', 'SENAS_OBLIGATORIAS_PENDIENTES', 'NIVEL_ANTERIOR_BLOQUEADO']],
        L4: ['BLOQUEADO', ['XP_INSUFICIENTE', 'NIVEL_ANTERIOR_BLOQUEADO']],
      });
    });
    it('datos de progreso y XP de sobra para niveles altos no permiten saltar un nivel intermedio bloqueado', () => {
      // XP de sobra y L2 "perfecto", pero L1 sin hacer: L2 bloqueado, y L3/L4 también aunque sus propias condiciones se cumplan.
      const p = player({ totalXp: 5000, progress: { s4: ok(100) } });
      expect(summary(LEVELS, p)).toEqual({
        L1: ['ACTUAL', []],
        L2: ['BLOQUEADO', ['SENAS_OBLIGATORIAS_PENDIENTES']],
        L3: ['BLOQUEADO', ['NIVEL_ANTERIOR_BLOQUEADO']],
        L4: ['BLOQUEADO', ['NIVEL_ANTERIOR_BLOQUEADO']],
      });
    });
    it('cumple todo hasta L2 pero no tiene la XP de L3: L2 abierto, L3 y L4 bloqueados por motivos distintos', () => {
      const p = player({ totalXp: 100, progress: { s1: ok(80), s2: ok(60) } });
      expect(summary(LEVELS, p)).toEqual({
        L1: ['ACTUAL', []],
        L2: ['DESBLOQUEADO', []],
        L3: ['BLOQUEADO', ['XP_INSUFICIENTE', 'SENAS_OBLIGATORIAS_PENDIENTES']],
        L4: ['BLOQUEADO', ['XP_INSUFICIENTE', 'NIVEL_ANTERIOR_BLOQUEADO']],
      });
    });
    it('los motivos siempre salen en el mismo orden estable', () => {
      const p = player({ totalXp: 0, progress: { s1: { completada: false, mejorPrecision: 0 } } });
      expect(summary(LEVELS, p).L2[1]).toEqual(['XP_INSUFICIENTE', 'SENAS_OBLIGATORIAS_PENDIENTES', 'PRECISION_INSUFICIENTE']);
      expect(summary(LEVELS, p).L3[1]).toEqual(['XP_INSUFICIENTE', 'SENAS_OBLIGATORIAS_PENDIENTES', 'NIVEL_ANTERIOR_BLOQUEADO']);
    });
  });

  describe('orden de los niveles', () => {
    it('la progresión sigue `orden`, no el orden del arreglo (inserción) ni el id', () => {
      const shuffled = [L3, L1, L4, L2];
      const result = evaluateLevelAccess(shuffled, player({ totalXp: 100, progress: { s1: ok(80), s2: ok(80) } }));
      expect(result.map((r) => r.levelId)).toEqual(['L1', 'L2', 'L3', 'L4']);
      expect(summary(shuffled, player({ totalXp: 100, progress: { s1: ok(80), s2: ok(80) } })).L2).toEqual(['DESBLOQUEADO', []]);
    });
    it('los huecos en orden/numero no importan: el anterior es el que precede inmediatamente por orden', () => {
      const gaps = [L('a', 1, 5, 0, 60), L('b', 7, 500, 0, 60), L('c', 40, 9000, 0, 60)];
      const p: PlayerState = { totalXp: 0, nivelActual: 7, mandatorySenasByLevel: new Map([['a', ['x']]]), progressBySena: new Map() };
      expect(summary(gaps, p)).toEqual({
        a: ['DESBLOQUEADO', []],
        b: ['BLOQUEADO', ['SENAS_OBLIGATORIAS_PENDIENTES']], // el anterior de `b` es `a` (orden 5)
        c: ['BLOQUEADO', ['NIVEL_ANTERIOR_BLOQUEADO']],
      });
    });
    it('si numero y orden discrepan, manda `orden` (y el estado ACTUAL usa numero)', () => {
      // numero 2 tiene orden 10 (primero) y numero 1 tiene orden 20 (segundo).
      const odd = [L('first', 2, 10, 0, 60), L('second', 1, 20, 0, 60)];
      const p: PlayerState = { totalXp: 0, nivelActual: 1, mandatorySenasByLevel: new Map([['first', ['x']]]), progressBySena: new Map() };
      expect(summary(odd, p)).toEqual({
        first: ['DESBLOQUEADO', []],
        second: ['BLOQUEADO', ['SENAS_OBLIGATORIAS_PENDIENTES']],
      });
    });
    it('sin niveles devuelve una lista vacía', () => {
      expect(evaluateLevelAccess([], player())).toEqual([]);
    });
  });

  describe('pureza', () => {
    it('no modifica los datos recibidos (ni escribe nada): entradas congeladas', () => {
      const frozenLevels = Object.freeze(LEVELS.map((l) => Object.freeze({ ...l })));
      const p = player({ totalXp: 100, progress: { s1: ok(80) } });
      const before = JSON.stringify({ frozenLevels, xp: p.totalXp, progress: [...p.progressBySena], mandatory: [...p.mandatorySenasByLevel] });
      expect(() => evaluateLevelAccess(frozenLevels, p)).not.toThrow();
      expect(JSON.stringify({ frozenLevels, xp: p.totalXp, progress: [...p.progressBySena], mandatory: [...p.mandatorySenasByLevel] })).toBe(before);
    });
    it('es determinista', () => {
      const p = player({ totalXp: 100, progress: { s1: ok(80), s2: ok(61) } });
      expect(evaluateLevelAccess(LEVELS, p)).toEqual(evaluateLevelAccess(LEVELS, p));
    });
  });
});

describe('sortByProgression / findNumberingInconsistencies', () => {
  it('ordena por orden sin mutar la entrada', () => {
    const input = [L3, L1, L2];
    expect(sortByProgression(input).map((l) => l.id)).toEqual(['L1', 'L2', 'L3']);
    expect(input.map((l) => l.id)).toEqual(['L3', 'L1', 'L2']);
  });
  it('no detecta nada cuando numero crece con orden, aunque haya huecos', () => {
    expect(findNumberingInconsistencies(LEVELS)).toEqual([]);
    expect(findNumberingInconsistencies([L('a', 1, 5, 0, 0), L('b', 9, 50, 0, 0)])).toEqual([]);
  });
  it('detecta parejas consecutivas por orden cuyo numero no crece', () => {
    const odd = [L('x', 5, 10, 0, 0), L('y', 2, 20, 0, 0), L('z', 3, 30, 0, 0)];
    expect(findNumberingInconsistencies(odd)).toEqual([{ previous: { orden: 10, numero: 5 }, current: { orden: 20, numero: 2 } }]);
  });
});
