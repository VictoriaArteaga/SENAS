import { applyDecorators } from '@nestjs/common';
import { ApiResponse } from '@nestjs/swagger';
import { ErrorResponseDto } from '../dto/error-response.dto';

/** Documents an endpoint's errors in Swagger: { status: 'CODE1 | CODE2' }. */
export function ApiErrors(errors: Record<number, string>) {
  return applyDecorators(
    ...Object.entries(errors).map(([status, codes]) =>
      ApiResponse({
        status: Number(status),
        description: `Codes: ${codes}`,
        type: ErrorResponseDto,
      }),
    ),
  );
}
