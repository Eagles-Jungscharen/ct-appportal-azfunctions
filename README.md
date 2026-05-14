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
    "OIDC_AUTHORITY": "",
    "OIDC_AUDIENCE": ""
  }
}
```

### Starten

```bash
dotnet build
func host start --port 7071
```

Das Backend ist dann unter `http://localhost:7071` erreichbar.

## API-Endpunkte

| Methode | Route | Beschreibung | Auth | Status |
|---|---|---|---|---|
| GET | `/api/me` | Eingeloggter Benutzer (`MeDto`) | Bearer | ❌ Nicht implementiert |
| GET | `/api/apps` | Apps des Benutzers (nach Token gefiltert) | Bearer | ❌ Nicht implementiert |
| GET | `/api/appmanagement/apps` | Alle registrierten Apps | Bearer + Admin | ❌ Nicht implementiert |
| POST | `/api/appmanagement/apps` | App erstellen | Bearer + Admin | ❌ Nicht implementiert |
| PUT | `/api/appmanagement/apps/{id}` | App bearbeiten | Bearer + Admin | ❌ Nicht implementiert |
| DELETE | `/api/appmanagement/apps/{id}` | App löschen | Bearer + Admin | ❌ Nicht implementiert |
| POST | `/api/appmanagement/apps/{id}/assignments` | Benutzer/Gruppen zuweisen | Bearer + Admin | ❌ Nicht implementiert |
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

### Hohe Priorität — Grundfunktionalität

| # | Thema | Beschreibung |
|---|---|---|
| 1 | **Alle Endpoints fehlen** | `GetApplications.cs` ist ein Placeholder (`"Welcome to Azure Functions!"`). Alle 8 API-Endpunkte müssen neu implementiert werden. |
| 2 | **Keine Authentifizierung** | Bearer-Token-Validierung fehlt vollständig. `AuthorizationLevel` ist aktuell `Anonymous`. Das Token muss gegen den Churchtool IDP (OIDC/JWT) validiert werden. |
| 3 | **Keine Autorisierung** | Admin-Checks (auf Basis von Gruppen/Rollen aus dem Token) fehlen. |
| 4 | **Kein Data Access Layer** | Kein Datenbank-Anschluss (kein EF Core, kein Cosmos DB, keine Datenmodelle). Unklar, ob SQL, CosmosDB oder ein anderer Speicher verwendet wird. |

### Mittlere Priorität

| # | Thema | Beschreibung |
|---|---|---|
| 5 | **Keine Service-Schicht** | Business Logic ist nicht getrennt — alles würde direkt in den Function-Klassen landen. |
| 6 | **Kein CORS** | CORS für `http://localhost:5173` (Dev) und die Produktions-URL ist nicht konfiguriert. |
| 7 | **Keine zentrale Fehlerbehandlung** | Unkontrollierte Exceptions würden als `500 Internal Server Error` ohne strukturierte Fehlermeldung ankommen. |
| 8 | **Keine C#-Datenmodelle** | DTOs existieren nur im Frontend (TypeScript). C#-Pendants fehlen. |
| 9 | **Integration Churchtool IDP** | `POST /api/appmanagement/clients` muss OAuth2-Clients beim Churchtool IDP registrieren — API-Kontrakt des IDP ist zu klären. |

### Niedrige Priorität

| # | Thema | Beschreibung |
|---|---|---|
| 10 | **Keine Tests** | Weder Unit- noch Integrationstests vorhanden. |
| 11 | **Kein OpenAPI/Swagger** | Keine API-Dokumentation generiert. |
| 12 | **Deployment-Konfiguration** | Kein `azure.yaml`, kein Bicep/Terraform, kein GitHub Actions Workflow. |
| 13 | **`local.settings.json` unvollständig** | Storage Connection String und IDP-Konfiguration fehlen. |
