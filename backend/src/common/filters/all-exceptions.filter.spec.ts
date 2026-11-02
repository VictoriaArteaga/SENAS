import {
  BadRequestException,
  HttpStatus,
  NotFoundException,
} from '@nestjs/common';
import { ThrottlerException } from '@nestjs/throttler';
import { ApiError, ErrorCode } from '../api-error';
import { AllExceptionsFilter } from './all-exceptions.filter';

function run(exception: unknown) {
  const json = jest.fn();
  const res = { status: jest.fn().mockReturnValue({ json }) };
  const req = {
    method: 'POST',
    url: '/api/v1/x?a=1',
    originalUrl: '/api/v1/x?a=1',
  };
  const host = {
    switchToHttp: () => ({ getRequest: () => req, getResponse: () => res }),
  };
  new AllExceptionsFilter().catch(exception, host as never);
  return {
    status: res.status.mock.calls[0][0] as number,
    body: json.mock.calls[0][0],
  };
}

describe('AllExceptionsFilter', () => {
  it('serializes ApiError with its code and without the query string in the path', () => {
    const { status, body } = run(
      new ApiError(HttpStatus.FORBIDDEN, ErrorCode.SIGN_LOCKED, 'Locked.'),
    );
    expect(status).toBe(403);
    expect(body).toMatchObject({
      statusCode: 403,
      code: 'SIGN_LOCKED',
      message: 'Locked.',
      path: '/api/v1/x',
    });
    expect(new Date(body.timestamp).toString()).not.toBe('Invalid Date');
    expect(body.details).toBeUndefined();
  });

  it('includes the validation details', () => {
    const { body } = run(
      new ApiError(
        HttpStatus.BAD_REQUEST,
        ErrorCode.VALIDATION_FAILED,
        'Invalid.',
        ['precision must not be greater than 1'],
      ),
    );
    expect(body.details).toEqual(['precision must not be greater than 1']);
  });

  it('maps generic HttpExceptions to a code per status', () => {
    expect(run(new NotFoundException('nothing')).body).toMatchObject({
      statusCode: 404,
      code: 'NOT_FOUND',
      message: 'nothing',
    });
  });

  it('turns an array of messages (class-validator) into details', () => {
    const { body } = run(new BadRequestException(['a', 'b']));
    expect(body).toMatchObject({
      code: 'VALIDATION_FAILED',
      details: ['a', 'b'],
    });
  });

  it('the rate limit is 429 TOO_MANY_REQUESTS', () => {
    const { status, body } = run(new ThrottlerException());
    expect(status).toBe(429);
    expect(body.code).toBe('TOO_MANY_REQUESTS');
  });

  it('unexpected errors are 500 without exposing internal details', () => {
    const error = new Error('password=secret in the SELECT ... query');
    const { status, body } = run(error);
    expect(status).toBe(500);
    expect(body).toMatchObject({
      code: 'INTERNAL_ERROR',
      message: 'Internal server error.',
    });
    expect(JSON.stringify(body)).not.toContain('secret');
  });
});
