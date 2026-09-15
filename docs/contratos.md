# Contratos deste repositório com os demais

Complementa `oficina-app/docs/contratos-entre-repositorios.md`. Cada item falha em
silêncio se divergir: o `apply` passa e o sintoma aparece longe da causa.

## O que este repositório HONRA (fixado pelo `oficina-app`)

| Item | Valor | Onde está no código |
|---|---|---|
| Token | `iss=oficina-auth`, `aud=oficina-api`, HS256, 60 min, claims `sub, perfil, documento, nome, jti, exp` | `Aplicacao/Tokens/ContratoDoToken.cs`, teste `EmissorDeTokenTestes.Emitir_TokenDeCliente_DeveSerAceitoPelaValidacaoDaApi` |
| `perfil` | `Cliente` / `Atendente` / `Admin`, case-sensitive; `documento` só em `Cliente` | `Dominio/Perfis.cs`, `EmissorDeToken` |
| Segredos | `oficina/jwt_secret` (≥ 32 chars), `oficina/db_password` — **texto puro** | `Aplicacao/Portas/NomesDeSegredos.cs`; `ProvedorDeSegredosSecretsManager` rejeita JSON |
| Banco | `oficina`, usuário `oficina_admin`, `clientes.cliente(id, nome, documento, ativo)`, `auth.usuario(id, username, password_hash, perfil, ativo)` | `Infraestrutura/Persistencia/*Npgsql.cs`; DDL espelho em `tests/.../BancoFixture.cs` |
| Hash de senha | BCrypt (`Senha.DeTextoPuro`, custo 12) | `Dominio/VerificadorDeSenha.cs` |
| `username` | minúsculo, `^[a-z0-9._]+$`, 3–50 | `Dominio/Username.cs` (cópia) |
| `Documento.cs` | cópia literal | job `paridade-documento` do CI |

## O que este repositório EXIGE do `oficina-infra-k8s`

| SSM / recurso | Uso aqui |
|---|---|
| `/oficina/apigw/api_id` | pendurar rotas e authorizer |
| `/oficina/apigw/vpc_link_integration_id` | **novo**: id da `aws_apigatewayv2_integration` (HTTP_PROXY via VPC Link) — alvo da rota `ANY /api/v1/{proxy+}` criada aqui (decisão D1) |
| `/oficina/network/vpc_id`, `/oficina/network/private_subnet_ids` (StringList) | `vpc_config` da auth-api |
| Rotas sem authorizer criadas lá | `GET /health`, `GET /swagger/{proxy+}`, `POST /api/v1/ordens-servico/{id}/orcamento/aprovacao` — mais específicas, vencem a `{proxy+}` |
| Throttling no stage `$default` | **Requisito**, não sugestão: `route_settings` para `POST /auth/cliente` e `POST /auth/admin` (`throttling_rate_limit = 10`, `throttling_burst_limit = 20`). É a única camada grossa; a fina (por IP/identidade) fica no DynamoDB deste repo e pode ser distribuída entre várias origens |
| Mapeamento opcional de contexto | `request_parameters` na integração do VPC Link: `append:header.X-Perfil = $context.authorizer.perfil`, `X-Sub`, `X-Documento`. Informativo — a API revalida o JWT |
| NAT Gateway | a auth-api sai para Secrets Manager e DynamoDB por ele (sem VPC endpoints) |
| Rota `ANY /api/v1/{proxy+}` | infra-k8s **não deve criar** essa rota — ela é criada aqui (ver tabela "Rotas que este repositório cria"). Dois `aws_apigatewayv2_route` disputando a mesma `route_key` em states diferentes dão `ConflictException` no `apply` |

## O que este repositório EXIGE do `oficina-infra-db`

| Item | Motivo |
|---|---|
| `/oficina/db/endpoint`, `/oficina/db/security_group_id` no SSM | conexão e regra de SG. `/oficina/db/endpoint` é **só o hostname, sem `:porta`** (`aws_db_instance.address`, não `.endpoint`) — vai direto para `Banco__Host` e a porta é configurada à parte |
| Regras do SG do RDS como **recursos separados** (`aws_vpc_security_group_ingress_rule`), nunca inline | este repo adiciona `5432 ← sg-lambda-auth` nesse SG; inline + separado no mesmo SG se apagam mutuamente a cada `apply` |
| `rds.force_ssl` | a auth-api usa `SSL Mode=Prefer`; funciona com `force_ssl` 0 ou 1 |

## O que este repositório PUBLICA

| SSM | Valor |
|---|---|
| `/oficina/auth/lambda_authorizer_id` | id do authorizer (informativo) |
| `/oficina/auth/api_function_name` | `oficina-auth-api` |

## Rotas que este repositório cria no API Gateway

| Rota | Integração | Authorizer |
|---|---|---|
| `POST /auth/cliente` | Lambda `oficina-auth-api` | — |
| `POST /auth/admin` | Lambda `oficina-auth-api` | — |
| `ANY /api/v1/{proxy+}` | VPC Link (id do SSM) | `oficina-auth-authorizer` (REQUEST, payload 2.0, simple response, cache 300 s) |

## Pendência resolvida aqui

Rate limiting / força bruta (pendência 7 do `oficina-app`): tabela DynamoDB
`oficina-auth-tentativas`, 10 falhas por IP e 5 por username a cada 15 min → 429.
