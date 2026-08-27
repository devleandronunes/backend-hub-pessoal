![header](https://capsule-render.vercel.app/api?type=waving&color=0:6366F1,100:22D3EE&height=180&section=header&text=Hub%20Pessoal%20%E2%80%93%20API&fontSize=42&fontColor=ffffff&animation=fadeIn)

<p align="center">
  <img src="https://readme-typing-svg.demolab.com?font=Fira+Code&size=18&pause=1000&color=6366F1&center=true&vCenter=true&width=560&lines=Clean+Architecture+em+.NET+10;Notas+versionadas+em+git%2C+de+verdade;Postgres+como+c%C3%B3pia+de+trabalho%2C+git+como+hist%C3%B3rico" alt="typing banner" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/C%23-13-239120?logo=csharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white" alt="PostgreSQL" />
  <img src="https://img.shields.io/badge/EF_Core-10-512BD4?logo=nuget&logoColor=white" alt="EF Core" />
  <img src="https://img.shields.io/badge/xUnit-Moq-25A162?logo=xunit&logoColor=white" alt="xUnit + Moq" />
  <img src="https://img.shields.io/badge/Testcontainers-Docker-2496ED?logo=docker&logoColor=white" alt="Testcontainers" />
  <img src="https://img.shields.io/badge/Swagger-OpenAPI-85EA2D?logo=swagger&logoColor=black" alt="Swagger" />
</p>

# Hub Pessoal — API

API do **Hub Pessoal**, um hub pessoal de notas, tarefas e cofre de arquivos. Este repositório cobre o **Módulo 1 — Sincronização Central**: autenticação, CRUD de notas e pastas em árvore, e um motor de sincronização bidirecional que espelha as notas do Postgres para um repositório git de verdade (GitHub).

Backend em **.NET 10** com **Clean Architecture**, banco **PostgreSQL** (Neon em produção, Docker em desenvolvimento), deploy no **Render**.

## Sumário

- [Arquitetura](#arquitetura)
- [Casos de uso](#casos-de-uso)
- [Fluxo do sistema — Notes](#fluxo-do-sistema--notes)
- [Uso das tecnologias](#uso-das-tecnologias)
- [Como rodar localmente](#como-rodar-localmente)
- [Como rodar os testes](#como-rodar-os-testes)
- [Documentação da API](#documentação-da-api)

## Arquitetura

Clean Architecture em quatro projetos, com dependência sempre apontando para dentro:

```
HubPessoal.Domain          → entidades puras (Note, NoteFolder, User, SyncCommit...), sem dependências externas
HubPessoal.Application     → casos de uso (Services), interfaces de repositório, contratos internos
HubPessoal.Infrastructure  → EF Core, repositórios, hashing de senha, cliente git
HubPessoal.Api             → minimal APIs, contratos HTTP, autenticação, Swagger
```

`Domain` não conhece ninguém. `Application` só conhece `Domain`. `Infrastructure` e `Api` conhecem `Application`, nunca o contrário — é por isso que os testes unitários (`HubPessoal.UnitTests`) conseguem testar toda a lógica de negócio mockando só interfaces, sem precisar de banco nem de git de verdade.

## Casos de uso

- **Autenticação** — login com usuário/senha (seed único, sem cadastro público), token JWT.
- **Notas e pastas em árvore** — criar, renomear, mover (com bloqueio de ciclo), fixar, duplicar, exportar `.md`, apagar (bloqueado se a pasta não estiver vazia).
- **Sincronização com git** — pré-visualizar o que mudou (`/sync/preview`), aplicar (`/sync/apply`, commit + pull + push), consultar histórico de commits (`/sync/history`). Sempre explícito: nada sincroniza sozinho em segundo plano.

## Fluxo do sistema — Notes

O Postgres é a cópia de trabalho (o que a UI lê e escreve o tempo todo); o repositório git é o histórico de verdade. Os dois só se encontram quando alguém aciona o sync — nunca automaticamente.

```mermaid
sequenceDiagram
    participant UI as Frontend
    participant API as HubPessoal.Api
    participant DB as Postgres
    participant WS as Workspace git local
    participant GH as GitHub

    UI->>API: POST /notes (criar nota)
    API->>DB: grava Note
    Note over DB: nota só existe no Postgres até aqui

    UI->>API: POST /sync/preview
    API->>WS: clona/atualiza workspace
    API->>DB: lê todas as notas e pastas
    API->>WS: materializa em notes/**/*.md (front matter + corpo)
    API->>WS: git add -A · git diff --cached
    API-->>UI: plano (arquivos, +inserções/-deleções, fingerprint)

    UI->>API: POST /sync/apply (fingerprint)
    API->>WS: git commit
    API->>GH: git pull --no-rebase && git push
    API->>DB: grava SyncCommit + SyncCommitFile (log do que foi enviado)
    API-->>UI: commitHash
```

Cada nota vira um arquivo `notes/<pasta>/<Título>.md` com front matter (`id`, `tags`, `pinned`, `createdAt`) e o corpo em markdown puro — por isso o repositório git continua legível e editável fora do Hub Pessoal. Quando alguém edita ou cria um arquivo direto no GitHub, o próximo `/sync/apply` faz o caminho inverso: `git pull` traz o commit, e o conteúdo é reimportado para o Postgres (`ImportWorkspaceIntoDatabaseAsync`), com deduplicação de título e remoção do que não existe mais na árvore de arquivos.

## Uso das tecnologias

| Tecnologia | Por quê |
| --- | --- |
| **.NET 10 / Minimal APIs** | Endpoints diretos em `Program.cs`, sem a cerimônia de controllers para uma API deste tamanho. |
| **EF Core + Npgsql** | Migrations versionadas, LINQ para as consultas de árvore/repositório. |
| **PostgreSQL** | Índices únicos (título por pasta) e `text[]` nativo para tags — motivo pelo qual os testes de integração usam Postgres real via Testcontainers, não um banco em memória. |
| **FluentValidation** | Validação de request separada da lógica de negócio, plugada como `EndpointFilter`. |
| **JWT Bearer + Argon2** | Autenticação stateless; Argon2id (via `Konscious.Security.Cryptography`) para hash de senha — mais resistente a força bruta em GPU que PBKDF2/bcrypt. |
| **Git via subprocess** | `SyncService` chama o binário `git` diretamente (clone/add/commit/pull/push) em vez de uma lib como LibGit2Sharp — o repositório de notas é git de verdade, então usar o próprio git elimina qualquer divergência de comportamento. |
| **xUnit + Moq** | Testes unitários dos `Services`, mockando repositórios e `IGitClient` — rodam em milissegundos, sem Docker. |
| **Testcontainers.PostgreSql** | Testes de integração sobem um Postgres efêmero real (imagem `postgres:16`, a mesma do dev) — pega erros que um banco em memória não pegaria. |
| **Swashbuckle (Swagger)** | Documentação interativa dos endpoints, disponível em desenvolvimento. |

## Como rodar localmente

1. Subir o Postgres de desenvolvimento:

   ```bash
   docker-compose up -d
   ```

2. Configurar os segredos locais (nunca em `appsettings.json`):

   ```bash
   cd HubPessoal.Api
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=<DB_DEV_NAME>;Username=<DB_USER>;Password=<DB_PASSWORD>"
   dotnet user-secrets set "Jwt:Key" "uma-chave-longa-o-suficiente"
   dotnet user-secrets set "Jwt:Issuer" "hub-pessoal"
   dotnet user-secrets set "Jwt:Audience" "hub-pessoal"
   dotnet user-secrets set "Auth:SeedUsername" "dev-user"
   dotnet user-secrets set "Auth:SeedPassword" "uma-senha-qualquer"
   dotnet user-secrets set "Git:RepositoryUrl" "https://github.com/<usuario>/personal-hub-notes-dev.git"
   dotnet user-secrets set "Git:Token" "<personal access token com escopo de repo>"
   dotnet user-secrets set "Git:AuthorName" "Hub Pessoal Dev"
   dotnet user-secrets set "Git:AuthorEmail" "dev@example.com"
   ```

   As chaves `Git:*` (Frente 12) são obrigatórias — a API falha ao subir se alguma faltar (`GitOptions.EnsureConfigured`).

3. Rodar a API:

   ```bash
   dotnet run --project HubPessoal.Api
   ```

   As migrations rodam automaticamente no startup, junto com o seed do usuário de desenvolvimento.

## Como rodar os testes

```bash
dotnet test HubPessoal.UnitTests         # milissegundos, sem Docker
dotnet test HubPessoal.IntegrationTests  # precisa de Docker rodando (Testcontainers sobe/derruba o Postgres sozinho)
```

Os testes de integração não dependem do Postgres de desenvolvimento nem do GitHub real — usam um container efêmero e um repositório git `bare` local, então podem rodar com `docker-compose` de dev parado.

## Documentação da API

Com a API em desenvolvimento, o Swagger fica em [`http://localhost:5208/swagger`](http://localhost:5208/swagger) (ajuste a porta conforme sua configuração).

---

<p align="center"><sub>Versão do módulo em <code>version.config</code>. Documentação de arquitetura e decisões de projeto vivem fora deste repositório.</sub></p>
