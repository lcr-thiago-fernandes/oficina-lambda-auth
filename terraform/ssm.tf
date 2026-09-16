resource "aws_ssm_parameter" "authorizer_id" {
  name        = "/${var.project}/auth/lambda_authorizer_id"
  description = "Id do Lambda Authorizer no API Gateway (informativo; a rota protegida ja e criada aqui)."
  type        = "String"
  value       = aws_apigatewayv2_authorizer.jwt.id
}

resource "aws_ssm_parameter" "api_function_name" {
  name  = "/${var.project}/auth/api_function_name"
  type  = "String"
  value = aws_lambda_function.api.function_name
}
