import { ValueTransformer } from 'typeorm';

/** `numeric` arrives from pg as a string; the entity uses it as a number. */
export const NumericTransformer: ValueTransformer = {
  to: (value?: number | null) => value,
  from: (value?: string | null) =>
    value === null || value === undefined ? null : parseFloat(value),
};
