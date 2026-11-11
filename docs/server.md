# HTTP server and remote client

`Lattice.Server` hosts the same `ILatticeClient` / `ILatticeSession` behavior used by the in-process frontends. `Lattice.Client.Http` implements that contract over the server API; callers keep ownership of the supplied `HttpClient`.

## Run the server

```sh
dotnet run --project src/Lattice.Server
```

The host uses ASP.NET Core URL configuration. For example:

```sh
dotnet run --project src/Lattice.Server -- --urls http://127.0.0.1:5080
```

By default, requests from loopback addresses are accepted and non-loopback requests are rejected. To permit remote clients, configure `LATTICE_SERVER_API_KEY` with a long random secret and bind the host to the desired interface:

```sh
LATTICE_SERVER_API_KEY="<long-random-secret>" dotnet run --project src/Lattice.Server -- --urls https://0.0.0.0:5080
```

Configure a valid Kestrel HTTPS certificate before exposing the service. Do not send the bearer key over plain HTTP or expose the server directly to an untrusted network. Put it behind a TLS-terminating reverse proxy or configure Kestrel TLS, and apply network-level access controls as appropriate.

## HTTP API

All routes are under `/api`. Requests and responses use JSON; enums are strings.

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/sessions` | Create an isolated session; returns `sessionId`. |
| POST | `/api/sessions/{id}/turns` | Submit `{"input":"..."}`; returns a structured turn and evidence. |
| POST | `/api/sessions/{id}/knowledge-packs` | Load `{"json":"..."}` into that session. |
| GET | `/api/sessions/{id}/status` | Read session counts. |
| DELETE | `/api/sessions/{id}` | Dispose and remove the session. |

Unknown session IDs return 404. When configured, the API key is supplied as an HTTP Bearer token on every request.

## Use the HTTP client

```csharp
using Lattice.Client;
using Lattice.Client.Http;

using var http = new HttpClient
{
    BaseAddress = new Uri("https://lattice.example/")
};

var client = new HttpLatticeClient(http, bearerToken: Environment.GetEnvironmentVariable("LATTICE_SERVER_API_KEY"));
await using ILatticeSession session = await client.CreateSessionAsync();

var turn = await session.SubmitAsync("Fido is a dog. Is Fido a dog?");
```

The session API and result types are the same as the in-process runtime. Session state is held in server memory; sessions are lost when the host stops, and idle-session expiration and durable persistence are not included in this first hosted slice.
