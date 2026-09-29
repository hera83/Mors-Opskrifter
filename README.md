# Mors Opskrifter

Et lille, selvhostet web-system til familiens opskrifter — så mors (og alle andres) klassikere er samlet ét sted, er lette at finde og kan deles med resten af familien.

## Funktioner

- **Opskrifter** med ingredienser, fremgangsmåde trin for trin, tider, antal personer, sværhedsgrad, noter og billede.
- **Kategorier** med ikoner, som kan administreres under Indstillinger.
- **Søgning** og **favoritter** for hver bruger.
- **PDF-eksport** af en opskrift, klar til at printe.
- **AI-import**: indsæt en URL til en opskrift, og siden bliver læst ind automatisk. Findes der struktureret opskriftsdata (Schema.org), bruges den direkte; ellers tolkes siden af en sprogmodel via [Ollama](https://ollama.com).
- **Brugere og roller**: `Administrator` kan oprette, redigere og slette; `User` kan læse og markere favoritter.
- **REST-API** (OpenAPI/Swagger) til at bruge opskrifterne i andre projekter, fx madplaner og indkøbslister.

Bygget med ASP.NET Core MVC (.NET 10), Entity Framework Core og SQLite. Hele brugerfladen er på dansk.

## Installation med Docker

Du skal bruge [Docker](https://docs.docker.com/get-docker/) med Docker Compose.

**1. Hent koden**

```bash
git clone https://github.com/hera83/Mors-Opskrifter.git
cd Mors-Opskrifter
```

**2. Opret konfigurationen**

Kopiér eksempelfilen til `.env`:

```bash
cp .env.example .env          # Linux/macOS
copy .env.example .env        # Windows
```

Åbn `.env` og udfyld værdierne:

| Variabel | Beskrivelse |
|---|---|
| `API_SHARED_KEY` | Nøgle til REST-API'et. Er den tom, er API'et lukket. Filen viser hvordan du genererer en. |
| `OLLAMA_BASE_URL` | Adressen på din Ollama-server, inkl. `/api` — fx `http://192.168.1.10:11434/api`. |
| `OLLAMA_API_KEY` | Kun hvis Ollama står bag en proxy der kræver nøgle. |
| `OLLAMA_CHAT_MODEL` / `OLLAMA_GENERATE_MODEL` | Modellen til AI-import, fx `gemma4:12b`. Den skal være hentet på serveren med `ollama pull <model>`. |

`.env` er udelukket fra git, så dine nøgler bliver hos dig. Ollama er kun nødvendig for AI-import — resten af systemet virker uden.

**3. Start**

```bash
docker compose up -d --build
```

Åbn **http://localhost:8080**.

**4. Opret den første bruger**

Første gang du åbner siden, bliver du sendt til en opsætningsguide, hvor du opretter den første bruger. Den bliver automatisk `Administrator`. Guiden kan kun bruges én gang; flere brugere oprettes derefter under Indstillinger → Brugere.

### Data og backup

Databasen (SQLite) og uploadede billeder gemmes i to Docker-volumes, `app_dbs` og `app_files`, så de overlever genstart og opdateringer. Tag en backup sådan:

```bash
docker compose stop
docker compose cp web:/app/App_dbs ./backup/App_dbs
docker compose cp web:/app/App_files ./backup/App_files
docker compose start
```

### Opdatering

```bash
git pull
docker compose up -d --build
```

Databasen opdateres automatisk ved opstart, og dine data bevares.

### HTTPS

Containeren kører HTTP på port 8080. Skal systemet være tilgængeligt udefra, så sæt det bag en reverse proxy med HTTPS (fx Caddy, Nginx Proxy Manager eller Traefik). Porten ændres i `docker-compose.yml` (`"8080:8080"` → `"<din port>:8080"`).

## REST-API

Alle opskrifter, ingredienser, fremgangsmåder og kategorier kan læses og administreres via et REST-API under `/api/v1`.

- **Swagger UI:** `http://localhost:8080/api/v1/swagger`
- **OpenAPI-dokument:** `http://localhost:8080/api/v1/openapi.json`

Alle kald kræver headeren `X-Api-Key` med værdien af `API_SHARED_KEY`:

```bash
curl -H "X-Api-Key: <din nøgle>" "http://localhost:8080/api/v1/recipes?category=Kager%20%26%20Desserter"
```

| Endpoint | Beskrivelse |
|---|---|
| `GET /api/v1/recipes` | Søg og filtrér (`search`, `category`, `categoryId`, `difficulty`, `maxTotalMinutes`, `ingredient`, `modifiedSince`, `sort`, `page`, `pageSize`) |
| `GET/POST/PUT/DELETE /api/v1/recipes/{id}` | Hent, opret, erstat og slet opskrifter |
| `PUT/DELETE /api/v1/recipes/{id}/image` | Upload eller fjern billede |
| `GET/POST/PUT/DELETE /api/v1/categories` | Administrér kategorier |
| `GET /api/v1/lookups` | Sværhedsgrader, kategorier, enheder og ingredienser til filtre |

## Lokal udvikling

Kræver [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
cd web
dotnet run
```

I udviklingstilstand bliver databasen fyldt med et par eksempelopskrifter første gang, og API-nøglen hentes fra `web/appsettings.Development.json`. Konfigurationen ligger i `web/appsettings.json`.

## Licens

[Creative Commons Attribution-NonCommercial 4.0](LICENSE.txt) — fri til personligt og ikke-kommercielt brug med kreditering.
