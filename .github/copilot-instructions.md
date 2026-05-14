# GitHub Copilot Instructions — ct-appportal-azfunctions

## Projektübersicht

Azure Functions Backend (.NET 10, Isolated Worker) für das ct-appportal. Stellt die REST-API bereit, über die das React-Frontend (`ct-appportal-ui`) Applikationen, Benutzerinformationen und OAuth2-Client-Registrierungen verwaltet.

Das Backend hat zwei Hauptverantwortlichkeiten:
- **App-Verwaltung**: CRUD für registrierte Applikationen und Zuweisung von Benutzern/Gruppen via Azure Table Storage
- **IDP-Integration**: Registrierung und Verwaltung von OAuth2-Clients beim Churchtool IDP via HTTP

## Tech-Stack

| Layer | Technologie |
|---|---|
| Runtime | .NET 10 (Isolated Worker) |
| Hosting | Azure Functions v4 |
| HTTP-Integration | ASP.NET Core (`Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`) |
| Telemetrie | Azure Application Insights |
| Table Storage | `GuedesPlace.AzureTools` v1.2.2 — `TypedAzureTableClient<T>` |

## Sprachkonventionen

- **Code**: Englisch (Klassen-, Methoden-, Variablennamen)
- **Kommentare im Code**: Deutsch (inline `//`-Kommentare)
- **Fehlermeldungen** (im `error`-Feld der JSON-Antwort): Deutsch

## Authentifizierung & Autorisierung

### Bearer Token Validierung

Alle Endpunkte sind über Bearer Token gesichert. Die Validierung erfolgt via **Standard OIDC JWT-Middleware** von ASP.NET Core gegen den OIDC Authority Endpoint des Churchtool IDP.

- `AuthorizationLevel` der Azure Functions: `Anonymous` (JWT wird manuell via Middleware validiert)
- Der Bearer Token wird im `Authorization: Bearer <token>` Header mitgeschickt
- Authority URL kommt aus der Konfiguration: `OIDC_AUTHORITY`

### JWT Claims

Das Churchtool IDP Token enthält nur folgende Claims:
- `sub` → `userId`
- `name` → `displayName`

`isAdmin` und `groups` sind **nicht** im Token und müssen separat von einer Churchtool-API geladen werden.
Der Endpoint ist noch offen — als `// TODO: CHURCHTOOL_USERINFO_URL konfigurieren` im Code markieren.
Der Konfigurationsschlüssel `CHURCHTOOL_USERINFO_URL` ist bereits reserviert.

### Admin-Prüfung

Admin-Checks erfolgen auf Basis von `isAdmin` aus dem Churchtool-API-Aufruf.
**Niemals** `isAdmin` aus JWT-Claims ableiten.

## API-Endpunkte

| Methode | Route | Beschreibung | Nur Admin |
|---|---|---|---|
| GET | `/api/me` | Eingeloggter Benutzer (`MeDto`) | — |
| GET | `/api/apps` | Apps des Benutzers (nach Gruppen gefiltert) | — |
| GET | `/api/appmanagement/apps` | Alle registrierten Apps | ✓ |
| POST | `/api/appmanagement/apps` | App erstellen | ✓ |
| PUT | `/api/appmanagement/apps/{id}` | App bearbeiten | ✓ |
| DELETE | `/api/appmanagement/apps/{id}` | App löschen | ✓ |
| POST | `/api/appmanagement/apps/{id}/assignments` | Benutzer/Gruppen zuweisen | ✓ |
| POST | `/api/appmanagement/clients` | OAuth2-Client beim Churchtool IDP registrieren | ✓ |

> **Wichtig**: Azure Functions reserviert `/api/admin` intern — daher `/api/appmanagement` verwenden.

## Datenzugriff (Azure Table Storage)

Apps und Zuweisungen werden in Azure Table Storage gespeichert. Der Zugriff läuft ausschliesslich über `TypedAzureTableClient<T>` aus dem `GuedesPlace.AzureTools.Tables`-Namespace.

### Registrierung im DI

```csharp
// In Program.cs via ExtendedAzureTableClientService registrieren
var tableService = new ExtendedAzureTableClientService(connectionString);
tableService.CreateAndRegisterTableClient<AppEntity>("Apps");
tableService.CreateAndRegisterTableClient<AppAssignmentEntity>("AppAssignments");
builder.Services.AddSingleton(tableService);
```

### Entity-Klassen

Jede Tabelle hat eine dedizierte Entity-Klasse (POCO). Keine Basisklasse oder Attribute nötig — nur public Properties mit Getter/Setter. Arrays und Listen werden automatisch als JSON-String serialisiert.

```csharp
// Beispiel App-Entity
public class AppEntity
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Url { get; set; }
    public string? IconUrl { get; set; }
    // Wird automatisch als JSON-Array-String serialisiert
    public List<string> RedirectUris { get; set; } = [];
    // Wird automatisch als JSON-Array-String serialisiert
    public List<string> RoleIds { get; set; } = [];
}
```

### CRUD-Muster

```csharp
// Alle Entities lesen
var results = await tableClient.GetAllAsync();
List<AppEntity> apps = results.Select(r => r.Entity).ToList();

// Einzelne Entity lesen
TableEntityResult<AppEntity>? result = await tableClient.GetByIdAsync(id);

// Upsert (Insert oder Replace)
await tableClient.InsertOrReplaceAsync(rowKey: entity.Id, partitionKey: "App", entity);

// Löschen
await tableClient.DeleteEntityAsync(rowKey: id, partitionKey: "App");
```

### Konfiguration

Der Connection String wird aus `AzureWebJobsStorage` (oder `TABLE_STORAGE_CONNECTION_STRING`) gelesen.

## Churchtool IDP Integration

OAuth2-Clients werden via HTTP-Calls an das [Churchtool IDP Backend](https://github.com/Eagles-Jungscharen/churchtool-idp-azfunctions) verwaltet. Basis-URL und Function Key kommen aus der Konfiguration.

### Konfigurationsschlüssel

```env
CHURCHTOOL_IDP_BASE_URL=        # Basis-URL des Churchtool IDP Backends
CHURCHTOOL_IDP_FUNCTION_KEY=    # x-functions-key Header-Wert
```

### Endpunkte des Churchtool IDP

| Methode | Route | Beschreibung |
|---|---|---|
| POST | `/api/clients` | Neuen OAuth2-Client erstellen |
| PUT | `/api/clients/{clientId}` | Client bearbeiten |
| DELETE | `/api/clients/{clientId}` | Client löschen |
| GET | `/api/clients` | Alle Clients auflisten |

Alle Requests werden mit dem Header `x-functions-key: <CHURCHTOOL_IDP_FUNCTION_KEY>` gesendet.

### Client-Registrierung

Request Body für `POST /api/clients`:
```json
{
  "name": "App-Name",
  "owner": "<userId aus JWT sub-Claim>",
  "redirectUris": ["https://..."]
}
```

- **`owner`** = `sub`-Claim (userId) des eingeloggten Admins aus dem JWT Token
- Das `clientSecret` wird nach der Registrierung einmalig zurückgegeben und darf **nicht** persistiert werden

### HTTP-Client Registrierung (DI)

```csharp
// Named Client "ChurchtoolIdp" mit BaseAddress und Function Key konfigurieren
services.AddHttpClient("ChurchtoolIdp", client =>
{
    client.BaseAddress = new Uri(config["CHURCHTOOL_IDP_BASE_URL"]!);
    client.DefaultRequestHeaders.Add("x-functions-key", config["CHURCHTOOL_IDP_FUNCTION_KEY"]);
});
```

## Fehlerbehandlung

### Antwortformat

Fehlermeldungen folgen dem gleichen Format wie das Churchtool IDP Backend:

```json
{
  "error": "Die Applikation wurde nicht gefunden.",
  "errorNumber": 1101
}
```

- `error`: Lesbare Fehlermeldung auf **Deutsch**
- `errorNumber`: Eindeutige numerische Fehler-ID

### Error-Nummern Konvention

| Bereich | Nummernbereich |
|---|---|
| Allgemein / Validierung | 1000–1099 |
| Applikations-Management | 1100–1199 |
| Zuweisungen | 1200–1299 |
| IDP / Client-Registrierung | 1300–1399 |

### HTTP-Statuscodes

| Situation | Statuscode |
|---|---|
| Validierungsfehler | 400 |
| Nicht authentifiziert | 401 |
| Nicht autorisiert (kein Admin) | 403 |
| Ressource nicht gefunden | 404 |
| Interner Fehler | 500 |

## Projektstruktur (Konvention)

```
ct-appportal-azfunctions/
├── Functions/
│   ├── MeFunction.cs                  # GET /api/me
│   ├── AppsFunction.cs                # GET /api/apps
│   └── AppManagementFunction.cs       # /api/appmanagement/*
├── Services/
│   ├── IAppService.cs / AppService.cs
│   ├── IAssignmentService.cs / AssignmentService.cs
│   └── IChurchtoolIdpService.cs / ChurchtoolIdpService.cs
├── Models/
│   ├── Entities/                      # Table Storage Entity-Klassen (class)
│   │   ├── AppEntity.cs
│   │   └── AppAssignmentEntity.cs
│   ├── Dtos/                          # Response DTOs (record, camelCase JSON)
│   │   ├── MeDto.cs
│   │   ├── AppDto.cs
│   │   ├── RoleDto.cs
│   │   ├── GroupAssignmentDto.cs
│   │   ├── ClientRegistrationDto.cs
│   │   ├── ClientRegistrationResultDto.cs
│   │   └── ErrorRecord.cs
│   └── Requests/                      # Eingehende Request Bodies (record)
│       ├── CreateAppRequest.cs
│       ├── UpdateAppRequest.cs
│       └── AssignGroupsRequest.cs
├── Middleware/
│   └── JwtValidationMiddleware.cs
├── Program.cs
├── host.json
└── local.settings.json
```

### Konventionen

- Eine Function-Klasse pro Ressource (nicht pro HTTP-Methode)
- Services via Interface über DI injizieren — nie direkt instanziieren
- `IHttpClientFactory` für alle ausgehenden HTTP-Calls verwenden
- `record` für DTOs und Request-Klassen
- `class` für Entity-Klassen (Table Storage)
- Keine Business-Logik in Function-Klassen — nur HTTP-Binding und Delegation an Services

## Konfiguration (local.settings.json)

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "",
    "OIDC_AUTHORITY": "",
    "CHURCHTOOL_IDP_BASE_URL": "",
    "CHURCHTOOL_IDP_FUNCTION_KEY": "",
    "CHURCHTOOL_USERINFO_URL": ""
  }
}
```

## Offene Punkte (TODOs)

| # | Thema | Beschreibung |
|---|---|---|
| 1 | **Gruppen/isAdmin Endpoint** | Welcher Churchtool-API-Endpoint liefert `isAdmin` und `groups` für einen Benutzer? Konfigurationsschlüssel `CHURCHTOOL_USERINFO_URL` ist reserviert. |
| 2 | **Gruppen-Filterung** | Logik, nach welchen Gruppen/Rollen Apps für `GET /api/apps` gefiltert werden, muss definiert werden. |
