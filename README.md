# 🛒 E-commerce Microservices (Minimal .NET 8)

![.NET](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet)  
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-Messaging-orange?logo=rabbitmq)  
![SQL Server](https://img.shields.io/badge/SQL%20Server-Database-red?logo=microsoftsqlserver)  
![Docker](https://img.shields.io/badge/Docker-Compose-blue?logo=docker)  
![Tests](https://img.shields.io/badge/Tests-xUnit-green?logo=xunit)  

Este projeto implementa uma **arquitetura de microserviços** simples para um e-commerce, utilizando **.NET 8**, **RabbitMQ**, **SQL Server**, **YARP API Gateway** e **JWT** para autenticação.

---

## 📐 Arquitetura

```mermaid
flowchart LR
    Client[Cliente/API Consumer] --> Gateway[API Gateway (YARP)]
    Gateway --> Inventory[InventoryService]
    Gateway --> Sales[SalesService]
    Sales -- Valida Estoque --> Inventory
    Sales -- Publica Evento --> MQ[(RabbitMQ)]
    MQ -- Consome Evento --> Inventory
    Inventory --> DB1[(InventoryDb - SQL Server)]
    Sales --> DB2[(SalesDb - SQL Server)]
Serviços
InventoryService 🏷️ — catálogo e estoque

SalesService 📦 — pedidos e integração com estoque

ApiGateway 🌐 — ponto de entrada centralizado (YARP)

RabbitMQ 📨 — eventos assíncronos (order.confirmed)

SQL Server 💾 — banco relacional, 2 bases (InventoryDb, SalesDb)

🔒 Autenticação
JWT (HS256) obrigatório em todos os endpoints, exceto:

/health

/swagger

/auth/login

Login de demonstração
json
Copiar código
POST /auth/login
{
  "username": "demo",
  "password": "demo"
}
Retorno:

json
Copiar código
{ "token": "<jwt-token>" }
Uso:

makefile
Copiar código
Authorization: Bearer <token>
🚀 Quick Start
1. Subir a stack
bash
Copiar código
docker compose up -d --build
2. Obter um token
bash
Copiar código
curl -s -X POST http://localhost:8080/inventory/auth/login \
  -H "content-type: application/json" \
  -d '{"username":"demo","password":"demo"}'
3. Criar um produto
bash
Copiar código
TOKEN=<cole_o_token_aqui>

curl -s -X POST http://localhost:8080/inventory/api/products \
  -H "Authorization: Bearer $TOKEN" \
  -H "content-type: application/json" \
  -d '{"name":"Mouse","description":"Wireless","price":199.9,"quantity":10}'
4. Criar um pedido
bash
Copiar código
curl -s -X POST http://localhost:8080/sales/api/orders \
  -H "Authorization: Bearer $TOKEN" \
  -H "content-type: application/json" \
  -d '{"items":[{"productId":1,"quantity":2}]}'
5. Conferir estoque atualizado
bash
Copiar código
curl -s http://localhost:8080/inventory/api/products/1 \
  -H "Authorization: Bearer $TOKEN"
📊 Fluxo de Eventos
mermaid
Copiar código
sequenceDiagram
    participant Client as Cliente
    participant Gateway as API Gateway
    participant Sales as SalesService
    participant Inventory as InventoryService
    participant MQ as RabbitMQ

    Client->>Gateway: POST /sales/api/orders
    Gateway->>Sales: Criar pedido
    Sales->>Inventory: Valida estoque via HTTP
    Inventory-->>Sales: Estoque disponível
    Sales->>Sales: Salva pedido no banco
    Sales->>MQ: Publica evento order.confirmed
    MQ->>Inventory: Evento consumido
    Inventory->>Inventory: Reduz estoque
    Client->>Gateway: GET /inventory/api/products/{id}
    Gateway->>Inventory: Consulta produto
    Inventory-->>Client: Estoque atualizado
🛠️ Tecnologias
🟣 .NET 8 Minimal APIs

🧩 Entity Framework Core + SQL Server

📨 RabbitMQ (mensageria)

🌐 YARP (API Gateway)

📜 JWT (autenticação)

📊 Swagger / OpenAPI

📑 Serilog (logs estruturados)

✅ xUnit (testes)

📂 Estrutura
bash
Copiar código
src/
 ├── ApiGateway/        # Gateway centralizado
 ├── InventoryService/  # Serviço de Estoque
 └── SalesService/      # Serviço de Vendas
tests/
 └── SalesService.Tests # Testes unitários
docker-compose.yml      # Orquestração completa
🔎 Endpoints
InventoryService
GET /api/products → lista produtos

GET /api/products/{id} → consulta produto

POST /api/products → cadastra produto

PUT /api/products/{id} → atualiza produto

GET /api/products/{id}/availability?qty=QTD → checa disponibilidade

SalesService
GET /api/orders → lista pedidos

GET /api/orders/{id} → consulta pedido

POST /api/orders → cria pedido (validação + evento)

Comuns
POST /auth/login → login (JWT)

GET /health → healthcheck

GET /swagger → docs interativas

✅ Critérios Atendidos
✔ Cadastro de produtos
✔ Criação de pedidos com validação de estoque
✔ Comunicação assíncrona com RabbitMQ
✔ API Gateway roteando corretamente
✔ Autenticação JWT
✔ Logs estruturados (Serilog)
✔ Testes unitários básicos

📈 Extensões Futuras
💳 Microserviço de Pagamentos

🚚 Microserviço de Entregas/Logística

🔁 Resiliência com Polly (retry/circuit breaker)

🛡️ Autorização com roles/claims

📡 Observabilidade com Prometheus + Grafana

⚡ Agora você tem um mini e-commerce com microserviços pronto para rodar em Docker, com arquitetura escalável e boas práticas aplicadas!

yaml
Copiar código
