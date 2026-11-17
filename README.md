# MercadoBot 🛒

Bot para Telegram que reúne a lista de compras e as tarefas de casa em um só lugar. A API recebe as mensagens por webhook e guarda os dados no SQL Server.

## O que dá para fazer

- Adicionar itens à lista de compras, consultar o que falta comprar e marcar itens como comprados.
- Consultar o histórico de compras pelo menu.
- Cadastrar tarefas de casa escolhendo o dia da semana e consultar a rotina de segunda a domingo.
- Depois de salvar uma tarefa, ver a lista atualizada e voltar ao menu principal pelo botão.

## Demonstração

1. Envie `/start` no Telegram para abrir o menu.
2. Toque em **🏠 Tarefas de casa → ➕ Adicionar tarefa**.
3. Escreva **Colocar o lixo para fora** e escolha **Terça-feira**.
4. Confira a tarefa na lista atualizada e toque em **🏠 Menu principal** para voltar.

<!-- Depois de adicionar as capturas ao repositório, inclua aqui as imagens do menu, do cadastro e da lista atualizada. -->

## CI/CD

O código está no GitHub, a API está no Render e o banco SQL Server está no Somee.

1. **CI — GitHub Actions.** A cada `git push`, o workflow [CI do MercadoBot](.github/workflows/ci.yml) compila a API em modo Release e gera um pacote de publicação para download.
2. **CD — Render.** Para novos commits na `main`, o Render usa a opção **After CI Checks Pass**. Depois que as verificações do GitHub passam, ele monta a imagem pelo [Dockerfile](Dockerfile) e publica a nova versão automaticamente.
3. **Verificação.** O mesmo commit aparece com sucesso em [GitHub Actions](https://github.com/AnolinoJunior/MercadoBot/actions) e como **Live** em **Render → Deploys**.

O ZIP é um artefato da CI. O Render publica a partir do código usando o Dockerfile, então o processo não depende de upload manual. Quando uma mudança exige uma nova tabela ou outra alteração no banco, a migração do EF Core é revisada e aplicada separadamente antes de publicar a versão que depende dela.

## Tecnologias

| Camada | Tecnologia |
| --- | --- |
| Interface | Botões e comandos no Telegram |
| Backend | C# e ASP.NET Core Web API (.NET 8) |
| Persistência | Entity Framework Core e SQL Server (Somee) |
| Integração | Telegram Bot API via webhook |
| Hospedagem e automação | Render, Docker e GitHub Actions |

## Executar localmente

Você precisa do SDK .NET 8 e de um banco SQL Server para desenvolvimento. Configure estas variáveis no ambiente em que a API vai rodar:

| Variável | Valor |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | String de conexão do banco de desenvolvimento |
| `Telegram__BotToken` | Token do bot de teste |
| `Telegram__WebhookSecret` | Segredo usado no registro do webhook de teste |

Na raiz do repositório, execute:

```powershell
dotnet build .\MercadoBot.Api\MercadoBot.Api.csproj
dotnet run --project .\MercadoBot.Api\MercadoBot.Api.csproj
```

Se o banco de desenvolvimento ainda não tiver as tabelas, aplique as migrações do EF Core nesse banco. Para testar pelo Telegram, registre o webhook do seu bot de teste em um endereço HTTPS público que encaminhe as requisições para `/api/telegram/webhook` e use o mesmo segredo da configuração.
