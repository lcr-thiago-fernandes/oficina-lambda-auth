resource "aws_cloudwatch_log_group" "authorizer" {
  name              = "/aws/lambda/${local.nome_authorizer}"
  retention_in_days = var.log_retention_days
}

# FORA da VPC de proposito: so valida assinatura, nao toca no banco; ENI/cold start em
# VPC penalizariam toda requisicao protegida (design, secao 3).
resource "aws_lambda_function" "authorizer" {
  function_name = local.nome_authorizer
  description   = "Lambda Authorizer do API Gateway: valida JWT HS256 (iss/aud/exp)"
  role          = aws_iam_role.authorizer.arn

  runtime          = "dotnet8"
  architectures    = ["x86_64"]
  handler          = "OficinaAuth.Authorizer::OficinaAuth.Authorizer.Function::HandlerAsync"
  filename         = var.authorizer_zip_path
  source_code_hash = filebase64sha256(var.authorizer_zip_path)

  memory_size = var.authorizer_memory_mb
  timeout     = 5

  layers = local.newrelic_habilitado ? [var.newrelic_layer_arn] : []

  environment {
    variables = merge({
      NEW_RELIC_APP_NAME = "oficina-auth"
    }, local.newrelic_env)
  }

  tags = local.newrelic_habilitado ? { "NR.Apm.Lambda.Mode" = "true" } : {}

  depends_on = [aws_cloudwatch_log_group.authorizer, aws_iam_role_policy_attachment.authorizer_basic]
}
