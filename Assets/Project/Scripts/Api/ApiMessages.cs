using Lumina.Core;

namespace Lumina.Api
{
    /// <summary>Convierte un <see cref="ApiException"/> en un mensaje para el jugador, en el idioma activo (LocTable).</summary>
    public static class ApiMessages
    {
        public static string For(ApiException e)
        {
            if (e == null) return Loc.T("api.err.generic");
            switch (e.Kind)
            {
                case ApiErrorKind.Network: return Loc.T("api.err.network");
                case ApiErrorKind.Timeout: return Loc.T("api.err.timeout");
                case ApiErrorKind.NotConfigured: return Loc.T("api.err.notConfigured");
                case ApiErrorKind.Server: return Loc.T("api.err.server");
            }

            if (e.Code == ApiCodes.InvalidSession || e.Code == ApiCodes.Unauthorized) return Loc.T("api.err.sessionEnded");
            string key = "api.err." + e.Code;
            return Loc.Has(key) ? Loc.T(key) : Loc.T("api.err.generic");
        }
    }
}
