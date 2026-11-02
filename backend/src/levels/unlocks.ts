export enum SignStatus {
  LOCKED = 'LOCKED',
  AVAILABLE = 'AVAILABLE',
  PASSED = 'PASSED',
}

/** A level is unlocked once the player reaches the XP it requires. */
export function isLevelUnlocked(totalXp: number, requiredXp: number): boolean {
  return totalXp >= requiredXp;
}

/**
 * Status of each sign in a level, in order. In an unlocked level the first
 * sign is available and each following one unlocks when the previous one is
 * passed. If the level is locked, every sign is LOCKED.
 */
export function levelSignStatuses(
  signIdsInOrder: string[],
  passed: ReadonlySet<string>,
  levelOpen: boolean,
): SignStatus[] {
  return signIdsInOrder.map((id, i) => {
    if (!levelOpen) return SignStatus.LOCKED;
    if (passed.has(id)) return SignStatus.PASSED;
    if (i === 0 || passed.has(signIdsInOrder[i - 1])) {
      return SignStatus.AVAILABLE;
    }
    return SignStatus.LOCKED;
  });
}
