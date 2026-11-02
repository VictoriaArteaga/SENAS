import { SetMetadata } from '@nestjs/common';

export const STRICT_THROTTLE = 'strictThrottle';

/** Applies the strict rate limit (THROTTLE_AUTH_LIMIT) to register, login and refresh. */
export const StrictThrottle = () => SetMetadata(STRICT_THROTTLE, true);
