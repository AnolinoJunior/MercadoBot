# 🛒 MercadoBot

Bot de lista de compras integrado ao Telegram, desenvolvido com C#, ASP.NET Core 8 e SQL Server.

O projeto surgiu de uma necessidade do dia a dia: organizar os produtos que faltam em casa e compartilhar a lista de compras com outra pessoa, diretamente pelo Telegram.

## Funcionalidades

- Listas individuais em conversas privadas.
- Listas compartilhadas em grupos.
- Cadastro de vários produtos em uma mensagem.
- Quantidades inteiras por produto.
- Botões para adicionar produtos, consultar pendentes e acessar o histórico.
- Marcação individual de produtos como comprados.
- Marcação de todos os pendentes com confirmação.
- Histórico com data da compra.
- Opção de desfazer uma compra.
- Remoção definitiva de itens.
- Limpeza automática do histórico antigo durante o uso do bot.

Cada conversa possui sua própria lista, identificada pelo `ChatId`.

## Tecnologias

- C# e .NET 8
- ASP.NET Core Web API
- Entity Framework Core 8
- SQL Server
- Telegram Bot API
- HttpClient
- Swagger / OpenAPI
- Hospedagem na Somee com HTTPS

## Como funciona

O Telegram envia mensagens e cliques em botões para o endpoint:

```http
POST /api/telegram/webhook
```

A API valida a chave do webhook, processa a ação, consulta ou atualiza o banco e envia a resposta ao Telegram.

O projeto utiliza controllers para receber os eventos, serviços para processar as operações e EF Core para acessar o banco.

## Exemplos de uso

É possível adicionar vários produtos de uma vez:

```text
/adicionar 2 bolachas, 3 leites, café
```

Resultado:

```text
2x bolachas
3x leites
1x café
```

Sem quantidade informada, o bot considera uma unidade.

Também é possível tocar em **Adicionar item** e responder à pergunta do bot com os produtos separados por vírgula, ponto e vírgula ou quebra de linha.

### Comandos

| Comando | Ação |
|---|---|
| `/start` | Exibe o menu inicial |
| `/ajuda` | Mostra as instruções |
| `/adicionar produtos` | Adiciona produtos à lista |
| `/lista` | Consulta os itens pendentes |
| `/comprado codigo` | Marca um item como comprado |
| `/historico` | Consulta os comprados |
| `/desfazer codigo` | Devolve um item aos pendentes |
| `/remover codigo` | Exclui um item definitivamente |

Nos grupos, os comandos podem ser direcionados ao bot:

```text
/lista@Pouco_bot
```

Os botões permitem realizar as principais ações sem digitar comandos.

## Regras de negócio

- Cada conversa privada ou grupo possui uma lista independente.
- Participantes de um grupo podem alterar sua lista compartilhada.
- Ao marcar um item como comprado, toda a quantidade dele passa para o histórico.
- A opção **Comprar todos** considera todos os pendentes da conversa no momento da confirmação.
- Somente quem iniciou a operação pode confirmar ou cancelar a compra em lote.
- Desfazer uma compra preserva o produto e sua quantidade.
- Remover um item exclui o registro definitivamente.

### Retenção do histórico

Os registros são excluídos pelo mês da compra, a partir do início do terceiro mês seguinte:

| Mês da compra | Exclusão a partir de |
|---|---|
| Outubro | 1º de janeiro |
| Novembro | 1º de fevereiro |
| Dezembro | 1º de março |

A limpeza é executada quando a conversa interage com as rotinas de consulta ou processamento do bot. Não existe um agendamento independente.

Itens pendentes são preservados. As datas são armazenadas em UTC e apresentadas no horário de Brasília.

## Estrutura do projeto

```text
MercadoBot.Api/
├── Context/
├── Controllers/
├── DTOs/
├── Migrations/
├── Models/
├── Services/
├── Program.cs
└── appsettings.json
```

## Executando o projeto

### Pré-requisitos

- SDK do .NET 8
- SQL Server acessível
- Bot criado pelo @BotFather no Telegram
- Visual Studio ou outro editor compatível

### 1. Clonar o repositório

```bash
git clone https://github.com/AnolinoJunior/MercadoBot.git
cd MercadoBot/MercadoBot.Api
```

### 2. Configurar os segredos locais

No Visual Studio, clique com o botão direito no projeto e selecione **Gerenciar Segredos do Usuário**.

Configure:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "SUA_CONNECTION_STRING"
  },
  "Telegram": {
    "BotToken": "SEU_TOKEN",
    "WebhookSecret": "SUA_CHAVE_WEBHOOK"
  }
}
```

A chave do webhook deve utilizar somente letras sem acentos, números, `_` ou `-`.

Não versione os valores reais.

O nome de usuário do bot está definido no controller e no serviço de compras. Ao utilizar outro bot, substitua as referências a `Pouco_bot`.

### 3. Criar as tabelas

No Console do Gerenciador de Pacotes do Visual Studio, selecione o projeto `MercadoBot.Api` e execute:

```powershell
Update-Database
```

### 4. Executar

```bash
dotnet run
```

O Swagger fica disponível em `/swagger` no ambiente de desenvolvimento.

### 5. Configurar o webhook

Para receber eventos do Telegram, a API precisa estar acessível por uma URL HTTPS pública. Um endereço `localhost` não é acessível diretamente pelo Telegram.

Registre o webhook pelo método `setWebhook` da Telegram Bot API, informando:

- `url`: endereço público seguido de `/api/telegram/webhook`.
- `secret_token`: a mesma chave configurada na API.
- `allowed_updates`: `["message", "callback_query"]`.

Em produção, configure a conexão e as chaves no servidor. Os User Secrets locais não são enviados na publicação.

## Segurança

- Validação do cabeçalho `X-Telegram-Bot-Api-Secret-Token`.
- Consultas e alterações de itens limitadas ao `ChatId` da conversa.
- Uso de User Secrets durante o desenvolvimento.
- Credenciais reais devem permanecer fora do repositório.

## Limitações atuais

- Até 15 produtos por mensagem de inclusão.
- Consultas mostram até 15 registros, sem navegação entre páginas.
- Quantidades inteiras de 1 a 999.
- Pesos e quantidades decimais ainda não são suportados.
- Produtos repetidos podem ser cadastrados como registros separados.
- Mensagens antigas podem continuar exibindo botões de listas anteriores.
- Não há compra parcial de um item.
- A disponibilidade depende da hospedagem.

## Evoluções planejadas

- Paginação da lista e do histórico.
- Registro de preços e totais.
- Compra parcial por quantidade.
- Leitura de cupons fiscais enviados por foto, com conferência dos dados extraídos.
- Testes automatizados das principais regras de negócio.

## Autor

Desenvolvido por [Anolino Junior](https://github.com/AnolinoJunior).
