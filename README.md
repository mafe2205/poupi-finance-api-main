# Poupi Finance API

API do app de finanças pessoais Poupi. Feita em ASP.NET Core (.NET 10), com PostgreSQL acessado via Dapper, OCR de comprovantes com PaddleOCR e textos de insights gerados localmente pelo Ollama.

## Pré-requisitos

| Ferramenta | Versão | Para quê |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0 | Compilar e rodar a API |
| [PostgreSQL](https://www.postgresql.org/download/) | 13 ou superior (testado no 18) | Banco de dados |
| [Ollama](https://ollama.com/download) | 0.34 ou superior | Redação dos insights semanais (opcional para o resto da API) |

## Setup

### 1. Clonar o repositório

```bash
git clone https://github.com/mafe2205/poupi-finance-api.git
cd poupi-finance-api
```

### 2. Criar o banco de dados

Crie um banco vazio chamado `appfinances` (pelo pgAdmin ou pelo `psql`) e rode o script de criação das tabelas:

```bash
psql -h localhost -p 5433 -U postgres -d appfinances -v ON_ERROR_STOP=1 --single-transaction -f Database/init.sql
```

> No Windows, o instalador do PostgreSQL normalmente **não** coloca o `psql` no PATH. Use o caminho completo (ex.: `"C:\Program Files\PostgreSQL\18\bin\psql.exe"`, trocando `18` pela sua versão) ou abra o `Database/init.sql` no **Query Tool** do pgAdmin, conectado ao banco `appfinances`, e execute.

Todos os IDs são números sequenciais (`BIGINT`) gerados pelo banco.

Se o banco já existia, rode só os scripts numerados que ainda não foram aplicados, em ordem (ex.: `Database/007_insight_rule_code.sql`). O `init.sql` já inclui todos eles.

A connection string em `AppFinances-API/appsettings.json` usa a porta **5433**. Se o seu PostgreSQL estiver na porta padrão (5432) ou em outro host, ajuste no passo seguinte.

### 3. Configurar os segredos (user-secrets)

Senha do banco e chave do JWT **não ficam no repositório**. Em desenvolvimento, elas vão no user-secrets, que fica fora da pasta do projeto (`%APPDATA%\Microsoft\UserSecrets\`).

Na raiz do repositório:

```bash
dotnet user-secrets set "ConnectionStrings:PostgresConnection" "Host=localhost;Port=5433;Database=appfinances;Username=postgres;Password=SUA_SENHA" --project AppFinances-API
```

> O user-secrets substitui a connection string **inteira** do `appsettings.json`, então informe todos os campos, não só a senha.

Para gerar e salvar uma chave JWT aleatória de 64 bytes (PowerShell):

```powershell
$b = New-Object byte[] 64; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); dotnet user-secrets set "Jwt:Key" ([Convert]::ToBase64String($b)) --project AppFinances-API
```

A chave assina os tokens de login (HMAC-SHA256, mínimo de 32 bytes). Se ela mudar, todos os tokens emitidos deixam de valer.

> **Não pule este passo.** O `appsettings.json` traz uma chave de exemplo (`sua-chave-secreta-com-pelo-menos-32-caracteres`) que tem o tamanho mínimo, então a API sobe sem erro mesmo sem o user-secrets, mas assinando os tokens com uma chave pública. Ela serve só de placeholder: configure sempre a sua.

Também dá para editar esses valores pelo Visual Studio: botão direito no projeto **AppFinances-API** → **Gerenciar Segredos do Usuário**.

### 4. OCR (PaddleOCR)

Não precisa fazer nada: o PaddleOCR (PP-OCRv5, modelo latino) roda na CPU pelos pacotes `Sdcb.PaddleOCR`, e o modelo vem embutido no pacote NuGet, sem download em tempo de execução. Os runtimes nativos de Windows x64 e Linux x64 são restaurados com o projeto. O modelo é carregado uma vez, na primeira leitura, o que leva alguns segundos.

No Windows, se o OCR falhar ao carregar as bibliotecas nativas, instale o [Visual C++ Redistributable](https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist) x64.

Para sugerir a categoria, a API consulta a atividade principal (CNAE) do CNPJ lido no comprovante na [BrasilAPI](https://brasilapi.com.br), que publica os dados abertos da Receita Federal. Só o CNPJ da empresa é enviado, uma vez por CNPJ (o resultado fica em `CNPJ_ACTIVITIES`). Se a consulta falhar (sem internet, limite de requisições), o aviso vai para o log e a categoria é decidida sem a atividade; `CnpjLookupSettings:Enabled = false` desliga a consulta.

### 5. Ollama (insights com IA)

Instale o Ollama e baixe o modelo configurado:

```bash
ollama pull qwen3:4b
```

O Ollama precisa estar rodando em `http://localhost:11434`. Só os jobs de insights usam o Ollama, e eles rodam em background. Se ele não estiver disponível, o job registra o erro e pula o insight, e o resto da API funciona normalmente.

Os insights vêm de 32 regras determinísticas (metas, cartões, contas, fluxo, orçamento e uso do app), em `AppFinancesClassLibrary/Application/BehavioralInsights/Rules`; os códigos estão em `InsightRuleCodes.cs`. O código decide se o insight existe e calcula os números; o Ollama só escreve a frase. Três jobs de geração rodam uma vez por dia: um com as regras diárias (prazos, saldo, metas, limite do mês), um com as semanais (última semana fechada) e um com as mensais (último mês fechado). Um quarto job diário, sem Ollama, avalia se o usuário agiu depois de cada insight (`ACTION_TAKEN`). Rodar todo dia não duplica nada: cada insight guarda a regra (`RULE_CODE`) e a entidade (`RELATED_ENTITY_ID`), e a deduplicação por período e o cooldown de cada regra impedem a repetição.

Cada frase leva de 1 a 2 minutos para ser gerada (o qwen3 "pensa" antes de responder), por isso o timeout é de 300 s. Modelo, URL e timeout ficam em `OllamaSettings`, no `appsettings.json`.

### 6. Rodar a API

```bash
dotnet run --project AppFinances-API --launch-profile http
```

Ou abra o `AppFinances-API.slnx` no Visual Studio e rode com F5.

O Swagger fica em **http://localhost:5027/swagger** (só no ambiente Development).

### Rodar com Docker (opcional)

A imagem já inclui as bibliotecas nativas do PaddleOCR para Linux.

```bash
docker build -f AppFinances-API/Dockerfile -t poupi-api .

docker run -d --name poupi-api -p 8080:8080 \
  -e ConnectionStrings__PostgresConnection="Host=host.docker.internal;Port=5433;Database=appfinances;Username=postgres;Password=SUA_SENHA" \
  -e Jwt__Key="SUA_CHAVE_JWT" \
  -e OllamaSettings__BaseUrl="http://host.docker.internal:11434" \
  -v poupi-storage:/app/storage \
  poupi-api
```

- Dentro do contêiner, `localhost` é o próprio contêiner: use `host.docker.internal` para chegar ao PostgreSQL e ao Ollama da sua máquina.
- O volume `poupi-storage` guarda os comprovantes enviados. Sem ele, os arquivos se perdem quando o contêiner é recriado.
- A imagem roda em ambiente `Production`, então o Swagger fica desligado. Para ativar, passe `-e ASPNETCORE_ENVIRONMENT=Development`.

## Primeiros passos na API

Quase todas as rotas exigem token JWT. As exceções são o cadastro e o login:

1. **Criar um usuário:** `POST /api/User`

   ```json
   {
     "name": "Maria",
     "surname": "Silva",
     "birthDate": "1995-05-20",
     "email": "maria@example.com",
     "phone": "11999999999",
     "user": "maria",
     "password": "senha-segura"
   }
   ```

   O cadastro também cria o login e uma conta "Carteira" padrão, tudo numa única transação. E-mail ou username repetido retorna **409**.

2. **Fazer login:** `POST /api/Authentication/login`

   ```json
   { "identifier": "maria", "password": "senha-segura" }
   ```

   O `identifier` aceita username ou e-mail. A resposta traz o `token`.

3. **Usar o token:** envie o header `Authorization: Bearer <token>` nas demais requisições. No Swagger, clique em **Authorize** e cole **só o token**, sem o prefixo `Bearer`, que é adicionado automaticamente.

4. **Trocar a senha:** `POST /api/Authentication/change-password` com o token e o corpo `{ "currentPassword": "...", "newPassword": "..." }`. A nova senha precisa ter pelo menos 6 caracteres. Tokens emitidos antes da troca continuam válidos até expirar.

5. **Enviar um comprovante:** `POST /api/documents/upload` (multipart, campo `file`) com o header `Idempotency-Key` (ex.: um GUID gerado ao tirar a foto). Reenviar com a mesma chave devolve **200** com o documento já existente em vez de criar outro. Depois do OCR, `GET /api/documents/{id}` traz os itens da nota, a sugestão de transação e, em `reviewReasons`, por que o documento precisa de revisão (falha no OCR, baixa confiança, total não encontrado, soma dos itens diferente do total, categoria ambígua).

6. **Categorias e reclassificação:** `GET /api/categories/suggest?description=...` sugere a categoria de um lançamento manual com o mesmo classificador do OCR (`requiresConfirmation = true` quando a descrição é ambígua). `POST /api/transactions/reclassify` reclassifica só as transações cuja categoria veio do classificador; as escolhidas ou corrigidas pelo usuário nunca mudam.

7. **Insights atualizados:** criar, editar ou excluir uma transação agenda a atualização da análise. Insights cujos números mudaram ou que não se aplicam mais saem da listagem (`?includeOutdated=true` mostra todos, com `outdatedAt`/`outdatedReason`) e os atuais são gerados de novo. `POST /api/BehavioralInsights/refresh` faz isso na hora.

Cada usuário só acessa os próprios dados. Rotas que recebem o ID de um recurso de outra pessoa retornam **403**, e o `userId` enviado no corpo de um cadastro é ignorado: vale sempre o usuário do token. Usuários com papel `admin` acessam tudo.

## Configuração

| Chave | Onde fica | Descrição |
|---|---|---|
| `ConnectionStrings:PostgresConnection` | user-secrets | Connection string completa do PostgreSQL |
| `Jwt:Key` | user-secrets | Chave de assinatura dos tokens |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpirationMinutes` | `appsettings.json` | Parâmetros do token |
| `OcrSettings` | `appsettings.json` | Limite de confiança, tamanho máximo da imagem na detecção do PaddleOCR (`PaddleDetectionMaxSize`) e pasta onde os comprovantes são salvos |
| `OllamaSettings` | `appsettings.json` | URL, modelo e timeout do Ollama |
| `CnpjLookupSettings` | `appsettings.json` | Consulta da atividade do CNPJ na BrasilAPI: liga/desliga (`Enabled`), URL e timeout |
| `InsightRuleSettings` | `appsettings.json` | Limites que disparam os insights, agrupados por área (`Goals`, `Cards`, `Accounts`, `Flow`, `Budget`, `Usage`). Cada regra só roda depois de um histórico mínimo (dias desde a primeira transação): as que olham só a situação atual rodam desde o 1º dia; `MinHistoryDays` (14) libera as que comparam semanas ou estimam o ritmo de gastos; `OneMonthHistoryDays` (30), as que analisam o mês; `TwoMonthsHistoryDays` (60), as que comparam meses ou precisam de repetições |
| `ExpoPushSettings` | `appsettings.json` | Envio de notificações push para o app pelo Expo: URL, timeout e `AccessToken` (opcional; só é necessário se o projeto no Expo exigir token de acesso, e nesse caso vai no user-secrets) |
| `BackgroundJobs:ScheduledJobsEnabled` | `appsettings.json` / variável de ambiente | `false` desliga os jobs agendados de insights (útil em testes de integração). Padrão: `true` |

Em produção, use variáveis de ambiente no lugar do user-secrets, trocando `:` por `__`:

```bash
ConnectionStrings__PostgresConnection="Host=...;Port=...;Database=appfinances;Username=...;Password=..."
Jwt__Key="..."
```

## Estrutura

```
AppFinances-API/            # Projeto web: controllers, middleware, filtros, Program.cs
  storage/                  # Comprovantes enviados (criado em runtime, fora do git)
AppFinancesClassLibrary/
  Application/              # Serviços, DTOs e interfaces
  Domain/                   # Entidades, enums, validações
  Infrastructure/           # Repositórios (Dapper), OCR, Ollama, jobs em background
AppFinancesClassLibrary.Tests/  # Testes unitários (OCR/parsing, classificação, regras de insight; sem banco e sem Ollama)
AppFinances-API.IntegrationTests/  # Testes de ponta a ponta pelos endpoints, com PostgreSQL descartável (Testcontainers)
  Fixtures/                 # Imagens de comprovante usadas nos testes
Database/                   # Scripts SQL
```

Para rodar os testes:

```bash
dotnet test AppFinancesClassLibrary.Tests          # unitários, sem dependências
dotnet test AppFinances-API.IntegrationTests       # integração: precisa do Docker rodando
```

Os testes de integração sobem um PostgreSQL 16 num contêiner, criado com o `Database/init.sql`, e a API inteira em memória. O PaddleOCR roda de verdade sobre as imagens de `Fixtures/`. O Ollama e o push são trocados por stubs, então os testes conferem os números enviados à IA, não o texto que ela escreve. O banco local e o user-secrets não são usados.
