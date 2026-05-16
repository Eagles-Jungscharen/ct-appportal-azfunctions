# ct-appportal-azfunctions

Azure Functions Backend für das ct-appportal. Stellt die REST-API bereit, über die das Frontend Applikationen, Benutzerinformationen und OAuth2-Client-Registrierungen verwaltet.

## Überblick

Das Backend läuft als isolierter .NET 10 Azure Functions Worker und ist das einzige Gegenstück zum React-Frontend (`ct-appportal-ui`). Es übernimmt:

- Authentifizierung und Autorisierung via Bearer Token (Churchtool IDP)
- Verwaltung registrierter Applikationen (CRUD)
- Filterung der für einen Benutzer sichtbaren Apps anhand von Gruppen/Rollen
- Zuweisung von Benutzern und Gruppen zu Applikationen
- Registrierung von OAuth2-Clients beim Churchtool IDP

## Tech-Stack

| Layer | Technologie |
|---|---|
| Runtime | .NET 10 (Isolated Worker) |
| Hosting | Azure Functions v4 |
| HTTP-Integration | ASP.NET Core |
| Telemetrie | Azure Application Insights |

## Lokale Entwicklung

### Voraussetzungen

- .NET SDK 10.0
- Azure Functions Core Tools v4
- Azurite (lokaler Storage-Emulator) oder Azure Storage Account

### Umgebungsvariablen

`local.settings.json` befüllen:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "",
    "CHURCHTOOL_URL": "",
    "OIDC_AUTHORITY_URL": "",
    "CHURCHTOOL_IDP_STORAGE_CONNECTION_STRING": "",
    "CHURCHTOOL_IDP_BASE_URL": "",
    "CHURCHTOOL_IDP_FUNCTION_KEY": "",
    "CHURCHTOOL_ADMIN_GROUP_ID": ""
  }
}
```

| Variable | Pflicht | Beschreibung |
|---|---|---|
| `AzureWebJobsStorage` | ✓ | Azure Storage Connection String. Lokal: `UseDevelopmentStorage=true` (Azurite). |
| `FUNCTIONS_WORKER_RUNTIME` | ✓ | Muss `dotnet-isolated` sein. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | — | Application Insights Connection String für Telemetrie. Leer lassen für lokale Entwicklung. |
| `CHURCHTOOL_URL` | ✓ | Basis-URL der Churchtool-Instanz (z.B. `https://myorg.church.tools`). Wird für API-Calls an Churchtool verwendet. |
| `OIDC_AUTHORITY_URL` | ✓ | OIDC Authority URL des Churchtool IDP. Wird für die JWT-Validierung per OIDC-Discovery verwendet. |
| `CHURCHTOOL_IDP_STORAGE_CONNECTION_STRING` | ✓ | Azure Storage Connection String für die Login-Token-Tabelle des Churchtool IDP. Kann identisch mit `AzureWebJobsStorage` sein. |
| `CHURCHTOOL_IDP_BASE_URL` | ✓ | Basis-URL des Churchtool IDP Azure Functions Backends. Wird für die OAuth2-Client-Registrierung verwendet. |
| `CHURCHTOOL_IDP_FUNCTION_KEY` | ✓ | `x-functions-key` Header-Wert für Calls an das Churchtool IDP Backend. |
| `CHURCHTOOL_ADMIN_GROUP_ID` | ✓ | `DomainIdentifier` der Churchtool-Gruppe, deren Mitglieder als Admins behandelt werden. |

### Starten

```bash
dotnet build
func host start --port 7071
```

Das Backend ist dann unter `http://localhost:7071` erreichbar.

## API-Endpunkte

| Methode | Route | Beschreibung | Auth | Status |
|---|---|---|---|---|
| GET | `/api/me` | Eingeloggter Benutzer (`MeDto`) | Bearer | ✅ Implementiert |
| GET | `/api/apps` | Apps des Benutzers (nach Token gefiltert) | Bearer | ✅ Implementiert |
| GET | `/api/appmanagement/apps` | Alle registrierten Apps | Bearer + Admin | ✅ Implementiert |
| POST | `/api/appmanagement/apps` | App erstellen | Bearer + Admin | ✅ Implementiert |
| PUT | `/api/appmanagement/apps/{id}` | App bearbeiten | Bearer + Admin | ✅ Implementiert |
| DELETE | `/api/appmanagement/apps/{id}` | App löschen | Bearer + Admin | ✅ Implementiert |
| POST | `/api/appmanagement/apps/{id}/assignments` | Benutzer/Gruppen zuweisen | Bearer + Admin | ✅ Implementiert |
| POST | `/api/appmanagement/clients` | OAuth2-Client beim Churchtool IDP registrieren | Bearer + Admin | ❌ Nicht implementiert |

> **Hinweis**: Azure Functions reserviert `/api/admin` intern — daher wird `/api/appmanagement` verwendet.

## Datenmodelle (geplant)

```csharp
record MeDto(string UserId, string DisplayName, bool IsAdmin, string[] Groups);
record AppDto(string Id, string Name, string? Description, string Url, string? IconUrl, string[] RedirectUris, RoleDto[] Roles);
record RoleDto(string Id, string Name, string? Description);
record GroupAssignmentDto(string AppId, string[] GroupIds, string[] UserIds);
record ClientRegistrationDto(string AppId, string ClientName, string[] RedirectUris);
record ClientRegistrationResultDto(string ClientId, string ClientSecret);
```

## Projektstruktur (angestrebt)

```
ct-appportal-azfunctions/
├── Functions/
│   ├── MeFunction.cs              # GET /api/me
│   ├── AppsFunction.cs            # GET /api/apps
│   └── AppManagementFunction.cs   # /api/appmanagement/*
├── Services/
│   ├── IAppService.cs
│   ├── AppService.cs
│   ├── IAssignmentService.cs
│   └── AssignmentService.cs
├── Models/
│   ├── AppDto.cs
│   ├── MeDto.cs
│   └── …
├── Middleware/
│   └── BearerTokenValidationMiddleware.cs
├── Program.cs
├── host.json
└── local.settings.json
```

## Offene Punkte

### Hohe Priorität

| # | Thema | Status | Beschreibung |
|---|---|---|---|
| 1 | **OAuth2-Client-Registrierung (IDP)** | ⏳ Offen | `POST /api/appmanagement/clients` muss OAuth2-Clients an das Churchtool IDP Backend weiterleiten. `ClientRegistrationDto`/`ClientRegistrationResultDto` sind definiert. `IChurchtoolIdpService` und der zugehörige Function-Endpoint fehlen noch. |
| 2 | **CORS für Produktion** | ⏳ Offen | Lokal via `local.settings.json` konfiguriert (`http://localhost:5173`). Für Azure-Deployment fehlt eine `host.json`-CORS-Konfiguration mit der Produktions-URL. |
| 3 | **Zentrale Fehlerbehandlung** | ⏳ Offen | Keine Exception-Middleware. Unbehandelte Fehler landen als `500` ohne strukturiertes `ErrorRecord`-Format. |

### Niedrige Priorität

| # | Thema | Status | Beschreibung |
|---|---|---|---|
| 4 | **Keine Tests** | ⏳ Offen | Weder Unit- noch Integrationstests vorhanden. |
| 5 | **Kein OpenAPI/Swagger** | ⏳ Offen | Keine API-Dokumentation generiert. |
| 6 | **Deployment-Konfiguration** | ⏳ Offen | Kein `azure.yaml`, kein Bicep/Terraform, kein GitHub Actions Workflow. |

### Erledigt

| # | Thema | Beschreibung |
|---|---|---|
| ✅ | **Bearer Token Validierung** | `JwtValidationMiddleware` validiert JWT via OIDC-Discovery gegen `OIDC_AUTHORITY_URL`. |
| ✅ | **Admin-Erkennung** | `MeService` berechnet `isAdmin` via Gruppen-Abgleich mit `CHURCHTOOL_ADMIN_GROUP_ID`. |
| ✅ | **C#-Datenmodelle** | Alle DTOs, Entities und Request-Klassen sind definiert. |
| ✅ | **`GET /api/me`** | Implementiert mit Caching (5 min TTL), liefert `UserId`, `DisplayName`, `IsAdmin` und `Groups` (`GroupDto` mit `Id`/`Title`). |
| ✅ | **ChurchTools API-Integration** | `ChurchToolsClientFactory` mit Kiota-Client, Token-Provider via Azure Table Storage und `st_ref`-Middleware sind vollständig implementiert. |
| ✅ | **`GET /api/apps`** | Implementiert in `AppsFunction`. Filtert Apps nach Gruppen des eingeloggten Benutzers. |
| ✅ | **App-Management-Endpoints** | Alle 5 `AppManagementFunction`-Endpoints (`GET`, `POST`, `PUT`, `DELETE` für Apps sowie `POST` für Gruppen-Zuweisungen) sind implementiert. |
| ✅ | **Admin-Guard für `/api/appmanagement`** | `CheckAdminAsync`-Helper prüft Authentifizierung und Admin-Status (401/403) in allen `AppManagement`-Endpoints. |
| ✅ | **Table Storage für Apps/Assignments** | `TypedAzureTableClient<AppEntity>` und `TypedAzureTableClient<AppAssignmentEntity>` sind in `Program.cs` unter dem Key `"PortalStorage"` registriert. |
| ✅ | **Service-Schicht** | `IAppService`/`AppService` vollständig implementiert (inkl. Gruppen-Zuweisung). Assignment-Logik ist direkt in `AppService` integriert — kein separates `IAssignmentService` nötig. |
| ✅ | **Scaffold-Code entfernt** | Placeholder `GetApplications.cs` (Scaffold `"Welcome to Azure Functions!"`) wurde gelöscht. |
