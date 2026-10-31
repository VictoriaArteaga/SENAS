/** Envoltorio común de las respuestas de consulta: { "data": ... }. */
export interface DataResponse<T> {
  data: T;
}
