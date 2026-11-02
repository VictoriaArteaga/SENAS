import { isLevelUnlocked, levelSignStatuses, SignStatus } from './unlocks';

describe('isLevelUnlocked', () => {
  it('unlocks on reaching exactly the required XP (299 vs 300)', () => {
    expect(isLevelUnlocked(299, 300)).toBe(false);
    expect(isLevelUnlocked(300, 300)).toBe(true);
    expect(isLevelUnlocked(301, 300)).toBe(true);
  });

  it('a level requiring 0 XP is always unlocked', () => {
    expect(isLevelUnlocked(0, 0)).toBe(true);
  });
});

describe('levelSignStatuses', () => {
  const ids = ['a', 'b', 'c', 'd'];

  it('with no progress only the first sign is available', () => {
    expect(levelSignStatuses(ids, new Set(), true)).toEqual([
      SignStatus.AVAILABLE,
      SignStatus.LOCKED,
      SignStatus.LOCKED,
      SignStatus.LOCKED,
    ]);
  });

  it('passing k-1 unlocks k', () => {
    expect(levelSignStatuses(ids, new Set(['a']), true)).toEqual([
      SignStatus.PASSED,
      SignStatus.AVAILABLE,
      SignStatus.LOCKED,
      SignStatus.LOCKED,
    ]);
    expect(levelSignStatuses(ids, new Set(['a', 'b']), true)).toEqual([
      SignStatus.PASSED,
      SignStatus.PASSED,
      SignStatus.AVAILABLE,
      SignStatus.LOCKED,
    ]);
  });

  it('all passed', () => {
    expect(
      levelSignStatuses(ids, new Set(ids), true).every(
        (s) => s === SignStatus.PASSED,
      ),
    ).toBe(true);
  });

  it('a locked level leaves every sign locked, even if some are passed', () => {
    expect(
      levelSignStatuses(ids, new Set(['a']), false).every(
        (s) => s === SignStatus.LOCKED,
      ),
    ).toBe(true);
  });

  it('a level with no signs returns an empty list', () => {
    expect(levelSignStatuses([], new Set(), true)).toEqual([]);
  });
});
