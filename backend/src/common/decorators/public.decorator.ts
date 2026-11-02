import { SetMetadata } from '@nestjs/common';

export const IS_PUBLIC = 'isPublic';

/** Marks a route as open (no JWT required). */
export const Public = () => SetMetadata(IS_PUBLIC, true);
