import { Injectable, Logger, UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import { parseDurationToSeconds } from '../common/duration';
import { normalizeEmail, normalizeUsername } from '../common/normalize';
import { SessionsService } from '../sessions/sessions.service';
import { UsersService } from '../users/users.service';
import {
  JwtPayload,
  LoginResponse,
  LogoutAllResponse,
  MeResponse,
  RefreshResponse,
  RegisterResponse,
  TokenResponse,
} from './auth.types';
import { LoginDto } from './dto/login.dto';
import { RefreshTokenDto } from './dto/refresh-token.dto';
import { RegisterDto } from './dto/register.dto';
import { PasswordService } from './password.service';
import { constantTimeEqualHex, hashRefreshToken, issueRefreshToken, parseRefreshToken } from './token.util';

const INVALID_CREDENTIALS = 'Credenciales inválidas.';
// Mismo mensaje para cualquier fallo de renovación: no revela si la sesión existe, está revocada, etc.
const INVALID_REFRESH = 'Refresh token inválido o expirado.';

@Injectable()
export class AuthService {
  private readonly logger = new Logger(AuthService.name);
  private readonly accessSeconds: number;
  private readonly refreshSeconds: number;

  constructor(
    private readonly users: UsersService,
    private readonly sessions: SessionsService,
    private readonly passwords: PasswordService,
    private readonly jwt: JwtService,
    config: ConfigService,
  ) {
    // La validación de entorno ya garantizó el formato; getOrThrow evita arrancar sin valor.
    this.accessSeconds = parseDurationToSeconds(config.getOrThrow<string>('JWT_ACCESS_EXPIRES_IN'))!;
    this.refreshSeconds = parseDurationToSeconds(config.getOrThrow<string>('REFRESH_TOKEN_EXPIRES_IN'))!;
  }

  /** Crea la cuenta y su perfil. No inicia sesión: el cliente debe llamar a /auth/login. */
  async register(dto: RegisterDto): Promise<RegisterResponse> {
    const passwordHash = await this.passwords.hash(dto.password);
    const user = await this.users.createWithProfile({
      username: normalizeUsername(dto.username),
      email: normalizeEmail(dto.email),
      passwordHash,
      displayName: dto.displayName.trim(),
    });
    this.logger.log(`Cuenta creada: ${user.id}`);
    return { message: 'Cuenta creada correctamente. Ya puedes iniciar sesión.', user };
  }

  /** Credenciales -> access token JWT + refresh token + una fila nueva en sesiones. */
  async login(dto: LoginDto): Promise<LoginResponse> {
    const usuario = await this.users.findByEmail(normalizeEmail(dto.email));

    // Siempre se hace una verificación Argon2 (real o ficticia) y el rechazo es idéntico en los
    // tres casos: correo inexistente, contraseña incorrecta o usuario inactivo.
    const passwordOk = usuario
      ? await this.passwords.verify(usuario.passwordHash, dto.password)
      : await this.passwords.verifyAgainstDummy(dto.password);
    if (!usuario || !passwordOk || !usuario.isActive) {
      throw new UnauthorizedException(INVALID_CREDENTIALS);
    }

    // Vigencia ABSOLUTA: se fija aquí y las renovaciones no la extienden.
    const fechaExpiracion = new Date(Date.now() + this.refreshSeconds * 1000);
    const refresh = issueRefreshToken();
    await this.sessions.create({
      id: refresh.sessionId,
      usuarioId: usuario.id,
      tokenHash: refresh.tokenHash,
      fechaExpiracion,
      informacionDispositivo: dto.informacionDispositivo
        ? ({ ...dto.informacionDispositivo } as Record<string, string>)
        : undefined,
    });

    this.logger.log(`Sesión iniciada: ${usuario.id}`);
    return {
      ...(await this.buildTokens(usuario.id, usuario.email, refresh.token, fechaExpiracion)),
      user: {
        id: usuario.id,
        username: usuario.username,
        email: usuario.email,
        displayName: usuario.perfil?.displayName ?? usuario.username,
      },
    };
  }

  /**
   * Renueva los tokens con rotación del refresh token.
   *
   * Política de reutilización (conservadora): si se presenta un token cuyo hash NO es el vigente de
   * esa sesión —sea un token ya rotado (reutilización) o la solicitud perdedora de dos renovaciones
   * simultáneas— se REVOCA esa sesión y se rechaza. No se revocan las demás sesiones del usuario:
   * cada sesión es un dispositivo con su propio secreto, así que el compromiso de un token no implica
   * el de los demás; revocar todo permitiría escalar el daño. Un cliente legítimo debe serializar las
   * renovaciones (una a la vez) y, si falla, volver a iniciar sesión.
   */
  async refresh(dto: RefreshTokenDto): Promise<RefreshResponse> {
    const parsed = parseRefreshToken(dto.refreshToken);
    if (!parsed) throw new UnauthorizedException(INVALID_REFRESH);

    const session = await this.sessions.findForRefresh(parsed.sessionId);
    if (!session) throw new UnauthorizedException(INVALID_REFRESH);

    const presentedHash = hashRefreshToken(parsed.token);
    if (!constantTimeEqualHex(presentedHash, session.tokenHash)) {
      await this.revokeForReuse(session.id, 'token anterior');
      throw new UnauthorizedException(INVALID_REFRESH);
    }

    const now = new Date();
    if (session.fechaRevocacion !== null) throw new UnauthorizedException(INVALID_REFRESH);
    if (session.fechaExpiracion.getTime() <= now.getTime()) throw new UnauthorizedException(INVALID_REFRESH);
    if (!session.usuario.isActive) throw new UnauthorizedException(INVALID_REFRESH);

    const next = issueRefreshToken(session.id);
    const rotated = await this.sessions.rotate({
      id: session.id,
      currentHash: presentedHash,
      newHash: next.tokenHash,
      now,
    });
    if (!rotated) {
      // Otra solicitud rotó (o revocó) esta sesión entre la lectura y la escritura.
      await this.revokeForReuse(session.id, 'solicitud concurrente');
      throw new UnauthorizedException(INVALID_REFRESH);
    }

    return this.buildTokens(session.usuarioId, session.usuario.email, next.token, session.fechaExpiracion);
  }

  /**
   * Cierra la sesión del refresh token presentado. Idempotente: el resultado externo es el mismo
   * (sin cuerpo) si el token es válido, malformado, desconocido, ajeno o ya revocado. Solo el
   * secreto completo de esa sesión puede revocarla; no hay forma de apuntar a sesiones por id.
   */
  async logout(dto: RefreshTokenDto): Promise<void> {
    const parsed = parseRefreshToken(dto.refreshToken);
    if (!parsed) return;
    await this.sessions.revokeByToken(parsed.sessionId, hashRefreshToken(parsed.token));
  }

  /** Revoca todas las sesiones activas del usuario autenticado (identidad = sub del JWT). */
  async logoutAll(userId: string): Promise<LogoutAllResponse> {
    const sesionesRevocadas = await this.sessions.revokeAllForUser(userId);
    this.logger.log(`Sesiones revocadas (${sesionesRevocadas}) para ${userId}`);
    return { message: 'Se cerraron todas tus sesiones.', sesionesRevocadas };
  }

  /** Datos públicos de la cuenta autenticada. */
  async me(userId: string): Promise<MeResponse> {
    const me = await this.users.findMeById(userId);
    if (!me) throw new UnauthorizedException();
    return me;
  }

  private async revokeForReuse(sessionId: string, motivo: string): Promise<void> {
    const revoked = await this.sessions.revoke(sessionId);
    if (revoked) {
      // Solo identificadores no secretos: nunca el token, su secreto ni su hash.
      this.logger.warn(`Posible reutilización de refresh token (${motivo}): sesión ${sessionId} revocada`);
    }
  }

  private async buildTokens(
    userId: string,
    email: string,
    refreshToken: string,
    refreshExpiresAt: Date,
  ): Promise<TokenResponse> {
    const payload: JwtPayload = { sub: userId, email };
    return {
      tokenType: 'Bearer',
      accessToken: await this.jwt.signAsync(payload),
      expiresIn: this.accessSeconds,
      refreshToken,
      refreshTokenExpiresAt: refreshExpiresAt.toISOString(),
    };
  }
}
