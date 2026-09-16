# O API (HTTP API), o stage $default, o VPC Link e a integracao com o NLB pertencem ao
# oficina-infra-k8s. Este repositorio so PENDURA rotas e o authorizer no API existente.

# ---------- /auth/* -> auth-api ----------
resource "aws_apigatewayv2_integration" "auth_api" {
  api_id                 = data.aws_ssm_parameter.api_id.value
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.api.invoke_arn
  payload_format_version = "2.0"
  timeout_milliseconds   = 10000
}

resource "aws_apigatewayv2_route" "auth_cliente" {
  api_id    = data.aws_ssm_parameter.api_id.value
  route_key = "POST /auth/cliente"
  target    = "integrations/${aws_apigatewayv2_integration.auth_api.id}"
}

resource "aws_apigatewayv2_route" "auth_admin" {
  api_id    = data.aws_ssm_parameter.api_id.value
  route_key = "POST /auth/admin"
  target    = "integrations/${aws_apigatewayv2_integration.auth_api.id}"
}

resource "aws_lambda_permission" "apigw_invoca_auth_api" {
  statement_id  = "AllowApiGatewayInvokeAuthApi"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.api.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${local.api_execution_arn}/*/*/auth/*"
}

# ---------- authorizer ----------
resource "aws_apigatewayv2_authorizer" "jwt" {
  api_id                            = data.aws_ssm_parameter.api_id.value
  name                              = local.nome_authorizer
  authorizer_type                   = "REQUEST"
  authorizer_uri                    = aws_lambda_function.authorizer.invoke_arn
  authorizer_payload_format_version = "2.0"
  enable_simple_responses           = true
  identity_sources                  = ["$request.header.Authorization"]
  authorizer_result_ttl_in_seconds  = var.authorizer_cache_ttl_seconds
}

resource "aws_lambda_permission" "apigw_invoca_authorizer" {
  statement_id  = "AllowApiGatewayInvokeAuthorizer"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.authorizer.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${local.api_execution_arn}/authorizers/${aws_apigatewayv2_authorizer.jwt.id}"
}

# ---------- rota protegida da API (decisao D1) ----------
# Aponta para a integracao do VPC Link criada pelo infra-k8s. Rotas mais especificas
# criadas la (GET /health, GET /swagger/{proxy+}, POST .../orcamento/aprovacao) vencem
# esta, sem authorizer — comportamento nativo do HTTP API.
resource "aws_apigatewayv2_route" "api_protegida" {
  api_id             = data.aws_ssm_parameter.api_id.value
  route_key          = "ANY /api/v1/{proxy+}"
  target             = "integrations/${data.aws_ssm_parameter.vpc_link_integration_id.value}"
  authorization_type = "CUSTOM"
  authorizer_id      = aws_apigatewayv2_authorizer.jwt.id
}
