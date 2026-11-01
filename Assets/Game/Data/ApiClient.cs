using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using RobotBombero.Simulation;
using UnityEngine;
using UnityEngine.Networking;

namespace RobotBombero.Game
{
    public enum ApiStatus
    {
        /// <summary>El servidor respondió con éxito.</summary>
        Ok,
        /// <summary>El servidor respondió con un error (409 nombre repetido, 429 límite, ...). Ver HttpStatus.</summary>
        Rejected,
        /// <summary>No hubo respuesta: sin conexión, tiempo agotado o respuesta ilegible.</summary>
        ConnectionFailed
    }

    /// <summary>Resultado de una llamada: éxito, rechazo del servidor con su código, o fallo de conexión.</summary>
    public sealed class ApiResult<T>
    {
        public ApiStatus Status { get; private set; }
        public int HttpStatus { get; private set; }
        public string Error { get; private set; }
        public T Value { get; private set; }
        public bool IsOk { get { return Status == ApiStatus.Ok; } }

        public static ApiResult<T> Ok(T value, int httpStatus = 200)
        {
            return new ApiResult<T> { Status = ApiStatus.Ok, HttpStatus = httpStatus, Value = value };
        }

        public static ApiResult<T> Rejected(int httpStatus, string error)
        {
            return new ApiResult<T> { Status = ApiStatus.Rejected, HttpStatus = httpStatus, Error = error };
        }

        public static ApiResult<T> ConnectionFailed(string error)
        {
            return new ApiResult<T> { Status = ApiStatus.ConnectionFailed, Error = error };
        }
    }

    [Serializable]
    public class PlayerCreated
    {
        public long playerId;
        public string name;
        public string key;
    }

    [Serializable]
    public class StartedGame
    {
        public long gameId;
        public uint seed;
    }

    /// <summary>Resultado de la partida según el servidor. Rank es nulo si esta partida no tiene fila en la clasificación.</summary>
    public sealed class GameResult
    {
        public int Steps;
        public int Fires;
        public long Points;
        public string EndReason;
        public long? Rank;
    }

    [Serializable]
    public class LeaderboardRow
    {
        public int rank;
        public string playerName;
        public string packName;
        public long points;
        public int steps;
        public int fires;
        public string date;
    }

    /// <summary>Conversión de las respuestas JSON. JsonUtility no admite nulos ni arrays en la raíz.</summary>
    public static class ApiParsing
    {
        [Serializable] private class FinishDto { public int steps; public int fires; public long points; public string endReason; }
        [Serializable] private class RowsDto { public LeaderboardRow[] rows; }
        [Serializable] private class ErrorDto { public string error; }
        [Serializable] private class PackIdDto { public long packId; }
        [Serializable] private class NameBody { public string name; }
        [Serializable] private class PackBody { public string name; public string rulesHash; }
        // Los campos de Rule se envían con sus nombres; el servidor los lee sin distinguir mayúsculas.
        [Serializable] private class StartBody { public long packId; public Rule[] rules; }

        private static readonly Regex RankPattern = new Regex("\"rank\"\\s*:\\s*(null|-?[0-9]+)", RegexOptions.CultureInvariant);

        public static GameResult ParseFinish(string json)
        {
            var dto = JsonUtility.FromJson<FinishDto>(json);
            if (dto == null || string.IsNullOrEmpty(dto.endReason)) throw new ArgumentException("Respuesta de partida no válida.");

            // El resto de campos son números o el nombre de un enum: la búsqueda no puede confundirse con texto libre.
            long? rank = null;
            Match m = RankPattern.Match(json);
            if (m.Success && m.Groups[1].Value != "null") rank = long.Parse(m.Groups[1].Value);
            return new GameResult { Steps = dto.steps, Fires = dto.fires, Points = dto.points, EndReason = dto.endReason, Rank = rank };
        }

        public static LeaderboardRow[] ParseLeaderboard(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Clasificación vacía.");
            var dto = JsonUtility.FromJson<RowsDto>("{\"rows\":" + json.Trim() + "}");
            return dto == null || dto.rows == null ? new LeaderboardRow[0] : dto.rows;
        }

        public static StartedGame ParseStarted(string json)
        {
            var dto = JsonUtility.FromJson<StartedGame>(json);
            if (dto == null || dto.gameId <= 0) throw new ArgumentException("Respuesta de partida no válida.");
            return dto;
        }

        public static long ParsePackId(string json)
        {
            var dto = JsonUtility.FromJson<PackIdDto>(json);
            if (dto == null || dto.packId <= 0) throw new ArgumentException("Respuesta de pack no válida.");
            return dto.packId;
        }

        public static string ParseError(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<ErrorDto>(json).error; }
            catch (ArgumentException) { return null; }
        }

        public static string PlayerBody(string name) { return JsonUtility.ToJson(new NameBody { name = name }); }
        public static string PackBodyJson(string name, string rulesHash) { return JsonUtility.ToJson(new PackBody { name = name, rulesHash = rulesHash }); }
        public static string StartBodyJson(long packId, Rule[] rules) { return JsonUtility.ToJson(new StartBody { packId = packId, rules = rules }); }
    }

    public interface IApiClient
    {
        IEnumerator CreatePlayer(string name, Action<ApiResult<PlayerCreated>> done);
        /// <summary>Con serverPackId 0 crea el pack; si no, lo actualiza. Devuelve el id del pack en el servidor.</summary>
        IEnumerator SavePack(string playerKey, long serverPackId, string name, string rulesHash, Action<ApiResult<long>> done);
        IEnumerator DeletePack(string playerKey, long serverPackId, Action<ApiResult<bool>> done);
        IEnumerator StartGame(string playerKey, long serverPackId, Rule[] rules, Action<ApiResult<StartedGame>> done);
        IEnumerator FinishGame(string playerKey, long gameId, Action<ApiResult<GameResult>> done);
        IEnumerator GetLeaderboard(Action<ApiResult<LeaderboardRow[]>> done);
    }

    /// <summary>Cliente sobre UnityWebRequest con rutas relativas (funciona en cualquier host). Nunca escribe la clave en el log.</summary>
    public class ApiClient : IApiClient
    {
        private const int TimeoutSeconds = 10;
        private const string KeyHeader = "X-Player-Key";

        private readonly string _baseUrl;

        public ApiClient(string baseUrl = "")
        {
            _baseUrl = baseUrl ?? "";
        }

        public IEnumerator CreatePlayer(string name, Action<ApiResult<PlayerCreated>> done)
        {
            return Send("POST", "/api/players", null, ApiParsing.PlayerBody(name), done,
                json => JsonUtility.FromJson<PlayerCreated>(json));
        }

        public IEnumerator SavePack(string playerKey, long serverPackId, string name, string rulesHash, Action<ApiResult<long>> done)
        {
            string body = ApiParsing.PackBodyJson(name, rulesHash);
            if (serverPackId <= 0)
                return Send("POST", "/api/packs", playerKey, body, done, ApiParsing.ParsePackId);
            return Send("PUT", "/api/packs/" + serverPackId, playerKey, body, done, json => serverPackId);
        }

        public IEnumerator DeletePack(string playerKey, long serverPackId, Action<ApiResult<bool>> done)
        {
            return Send("DELETE", "/api/packs/" + serverPackId, playerKey, null, done, json => true);
        }

        public IEnumerator StartGame(string playerKey, long serverPackId, Rule[] rules, Action<ApiResult<StartedGame>> done)
        {
            return Send("POST", "/api/games", playerKey, ApiParsing.StartBodyJson(serverPackId, rules), done,
                ApiParsing.ParseStarted);
        }

        public IEnumerator FinishGame(string playerKey, long gameId, Action<ApiResult<GameResult>> done)
        {
            return Send("POST", "/api/games/" + gameId + "/finish", playerKey, null, done, ApiParsing.ParseFinish);
        }

        public IEnumerator GetLeaderboard(Action<ApiResult<LeaderboardRow[]>> done)
        {
            return Send("GET", "/api/leaderboard", null, null, done, ApiParsing.ParseLeaderboard);
        }

        private IEnumerator Send<T>(string method, string path, string playerKey, string body, Action<ApiResult<T>> done, Func<string, T> parse)
        {
            ApiResult<T> result;
            using (var request = new UnityWebRequest(_baseUrl + path, method))
            {
                request.timeout = TimeoutSeconds;
                request.downloadHandler = new DownloadHandlerBuffer();
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (!string.IsNullOrEmpty(playerKey)) request.SetRequestHeader(KeyHeader, playerKey);

                yield return request.SendWebRequest();

                string text = request.downloadHandler == null ? null : request.downloadHandler.text;
                int code = (int)request.responseCode;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try { result = ApiResult<T>.Ok(parse(text), code); }
                    catch (ArgumentException) { result = ApiResult<T>.ConnectionFailed("Respuesta ilegible."); }
                }
                else if (request.result == UnityWebRequest.Result.ProtocolError)
                {
                    result = ApiResult<T>.Rejected(code, ApiParsing.ParseError(text));
                }
                else
                {
                    result = ApiResult<T>.ConnectionFailed(request.error);
                }
            }
            if (done != null) done(result);
        }
    }
}
