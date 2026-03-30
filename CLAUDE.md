# AgilineeringApi

ASP.NET Core .NET 10 REST API — blogg med inlägg, taggar och JWT-autentisering.

## Tech Stack
- .NET 10, ASP.NET Core MVC (controllers)
- EF Core SQLite (`EnsureCreated`, ALTER TABLE shim för schema-ändringar)
- Microsoft.AspNetCore.Authentication.JwtBearer
- BCrypt.Net-Next
- xUnit + WebApplicationFactory (integrationstester)

## Projektstruktur
```
AgilineeringApi/
  Controllers/   AuthController, PostsController, TagsController
  Services/      IAuthService, IPostsService, ITagsService + impl, ServiceResult<T>
  Models/        User, Post, Tag
  Data/          AppDbContext
  Program.cs

AgilineeringApi.Tests/
  AgilineeringFactory.cs   WebApplicationFactory med SQLite :memory:
  AuthControllerTests.cs
  AuthLockoutTests.cs / AuthResetTests.cs
  PostsControllerTests.cs
  TagsControllerTests.cs
```

## API Endpoints
- `POST /auth/login` — publikt, returnerar JWT
- `GET /posts`, `GET /posts/{slug}` — publikt (admin ser även opublicerade)
- `POST /posts`, `PUT /posts/{id}`, `DELETE /posts/{id}` — kräver JWT (admin)
- `GET /tags` — publikt
- `POST /tags`, `DELETE /tags/{id}` — kräver JWT (admin)

## Arkitektur
- Controllers beror bara på interfaces, aldrig konkreta typer
- Services beror på AppDbContext och IConfiguration — aldrig på controllers
- JWT: HS256, 8h, claims: NameIdentifier (userId) + Name + Role
- Nyckel från `Jwt:Key` config (Fly.io-secret i produktion)
- `ServiceResult<T>` och `ServiceResult` för tjänsteresultat: Ok/NotFound/Forbidden/Conflict

## Kodstil
- C# primary constructors
- Nullable enabled — behandla null som tomt
- Structured logging med message templates (inte string interpolation)
- Alla async-metoder avslutas med `Async`

## Testkonfiguration
- SQLite :memory: med öppen connection (bevarar data inom factory-livslängd)
- `Security:LoginRateLimit` = 1000 i tester (avaktivera rate limiting effektivt)
- `Security:MaxFailedLoginAttempts` = 3 i tester (lägre tröskel för snabbare testning)
- Tester som muterar user-state (lockout) ligger i **separata klasser med egen fixture**

## Kommandon
- Testa: `& 'C:\Program Files\dotnet\dotnet.exe' test 'C:\Users\Rikard\AgilineeringApi\AgilineeringApi.Tests\AgilineeringApi.Tests.csproj'`
- Deploy: push till main → GitHub Actions bygger och deployar till Fly.io

## Infrastruktur
- GitHub repo: https://github.com/skelander/AgilineeringApi
- API: https://forwardagility-rikard.fly.dev
- SQLite-volym: `forwardagility_data` på `/data/forwardagility.db`
- Seedade användare: `admin` / `admin` (admin-roll)

---

# Kvalitetskrav

Dessa regler gäller **alltid** — vid ny kod, ändringar och kodgranskning.

## 1. Tester

### Integrationstester (obligatoriskt)
- Varje ny endpoint **måste** ha integrationstester i `AgilineeringApi.Tests`
- Täck lyckligt flöde + felflöden (401, 403, 404, 409 etc.)
- Tester ska verifiera **beteende**, inte implementation
- Testnamn: `Metod_Scenario_FörväntatResultat` (t.ex. `Login_AfterMaxFailedAttempts_ReturnsLocked`)

### Enhetstester (vid komplex logik)
- Icke-trivial affärslogik i services ska ha enhetstester
- Enkel CRUD-logik täcks av integrationstester — separata enhetstester inte nödvändiga

### Testisolering
- Tester som muterar delad state (t.ex. user-lockout) ska ligga i **separata testklasser** med egna `IClassFixture`
- Inga beroenden mellan tester — varje test ska klara sig självständigt
- Inga `Thread.Sleep` i tester

## 2. Clean Code

- **Enkelt ansvar** — varje klass/metod gör en sak
- **Metodlängd** — helst under 20 rader; bryt ut om logiken kräver kommentar för att förstås
- **Namngivning** — självdokumenterande namn; inga förkortningar som `mgr`, `svc`, `tmp`
- **Inga magiska värden** — konstanter eller konfiguration, aldrig hårdkodade strängar/siffror i logik
- **Inga kommenterade kodblockar** — ta bort, inte kommentera ut
- **DRY** — upprepa inte logik; bryt ut till hjälpmetod eller service
- **Kommentarer** förklarar *varför*, inte *vad* — om koden behöver förklaras är det ett tecken på att den ska skrivas om

## 3. Clean Architecture

- **Beroenderiktning**: Controllers → Services (interface) → Data
- Controllers anropar **bara** `IXxxService`-interfaces — aldrig `AppDbContext` direkt
- Services känner inte till controllers eller HTTP-begrepp
- **DTOs/records** separeras från domänmodeller — returtyper från services är records, inte EF-entiteter
- Inga circular dependencies

## 4. API-design

- HTTP-statuskoder används korrekt:
  - `200 OK` — lyckad GET/PUT
  - `201 Created` — lyckad POST (med Location-header om möjligt)
  - `204 No Content` — lyckad DELETE
  - `400 Bad Request` — ogiltig input
  - `401 Unauthorized` — ej autentiserad
  - `403 Forbidden` — autentiserad men saknar behörighet
  - `404 Not Found` — resursen finns inte
  - `409 Conflict` — duplikat/business rule-violation
  - `429 Too Many Requests` — rate limiting / lockout
- Felresponser har alltid formatet `{ "error": "beskrivning" }`
- Känslig info (lösenord, stacktrace) läcker **aldrig** i felmeddelanden

## 5. Säkerhet

- Validera all input vid systemgränser (controllers)
- Autentisering kontrolleras via `[Authorize]`-attribut — aldrig manuellt i service
- Lösenord hashas alltid med BCrypt — aldrig i klartext
- JWT-nyckel kommer alltid från konfiguration/miljövariabel — aldrig hårdkodad
- User enumeration undviks — samma felmeddelande för "fel användare" och "fel lösenord"

## 6. Async/Await

- Alla databasanrop är asynkrona (`await db.X.ToListAsync()` etc.)
- Inga `.Result` eller `.Wait()`
- Inga `async void` (undantag: event handlers)

## 8. Granskning före commit

**Obligatoriskt efter varje ny feature eller ändring, innan commit.**

Gå igenom dessa punkter för den kod som skrivits:

- [ ] **Säkerhet** — autentisering och auktorisering på rätt plats? Känslig data exponeras inte? Input valideras?
- [ ] **Felhantering** — alla felvägar hanteras? Undantag fångas på rätt nivå? Inga tomma `catch {}`?
- [ ] **Tester** — nytt beteende täckt? Felflöden testade? Testerna isolerade?
- [ ] **Async** — inga `.Result`/`.Wait()`? CancellationToken propageras?
- [ ] **Databasanrop** — N+1-frågor? `AsNoTracking()` på läsningar? Index täcker frågemönstret?
- [ ] **Konfiguration** — inga hårdkodade hemligheter, URL:er eller miljöspecifika värden?
- [ ] **CLAUDE.md** — behöver något uppdateras (endpoints, struktur, kommandon)?

Granskningen tar 2–3 minuter och är inte valfri.

## 7. Schema-ändringar (SQLite)

- `EnsureCreated` skapar schemat vid ny DB — migrations används inte
- Vid ny kolumn: lägg till ALTER TABLE-shim i `ApplySchemaChanges()` i `Program.cs`
- Shims är idempotenta (try/catch ignorerar "column already exists")
