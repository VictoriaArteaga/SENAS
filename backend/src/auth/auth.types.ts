import { MeProfile, PublicUser } from '../users/users.service';

export interface JwtPayload {
  sub: string;
  email: string;
  iat?: number;
  exp?: number;
}

/** Lo que JwtStrategy.validate deja en request.user (tras comprobar que la cuenta existe y está activa). */
export interface AuthenticatedUser {
  userId: string;
  email: string;
}

export interface RegisterResponse {
  message: string;
  user: PublicUser;
}

/** Tokens emitidos en login y en cada renovación. */
export interface TokenResponse {
  tokenType: 'Bearer';
  accessToken: string;
  /** Segundos de vigencia del access token. */
  expiresIn: number;
  /** Se entrega una sola vez; en la base solo se guarda su SHA-256. Cambia en cada renovación. */
  refreshToken: string;
  /** Vencimiento ABSOLUTO de la sesión: la renovación no lo extiende. */
  refreshTokenExpiresAt: string;
}

export interface LoginResponse extends TokenResponse {
  user: PublicUser;
}

export type RefreshResponse = TokenResponse;

export interface LogoutAllResponse {
  message: string;
  sesionesRevocadas: number;
}

export type MeResponse = MeProfile;
