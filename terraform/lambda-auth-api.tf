resource "aws_cloudwatch_log_group" "api" {
  name              = "/aws/lambda/${local.nome_api}"
  retention_in_days = var.log_retention_days
}

resource "aws_lambda_function" "api" {
  function_name = local.nome_api
  description   = "Emissor unico de JWT: POST /auth/cliente (CPF) e POST /auth/admin (usuario/senha)"
  role          = aws_iam_role.api.arn

  runtime       = "dotnet8"
  architectures = ["x86_64"]
  # Projeto ASP.NET Core com Amazon.Lambda.AspNetCoreServer.Hosting: o handler e o assembly executavel.
  handler          = "OficinaAuth.Api"
  filename         = var.api_zip_path
  source_code_hash = filebase64sha256(var.api_zip_path)

  memory_size = var.api_memory_mb
  timeout     = 10

  vpc_config {
    subnet_ids         = local.private_subnet_ids
    security_group_ids = [aws_security_group.lambda_auth.id]
  }

  layers = local.newrelic_habilitado ? [var.newrelic_layer_arn] : []

  environment {
    variables = merge({
      Banco__Host               = data.aws_ssm_parameter.db_endpoint.value
      Banco__Porta              = "5432"
      Banco__Nome               = "oficina"
      Banco__Usuario            = "oficina_admin"
      Segredos__Origem          = "SecretsManager"
      Tentativas__Armazenamento = "DynamoDb"
      Tentativas__Tabela        = aws_dynamodb_table.tentativas.name
      NEW_RELIC_APP_NAME        = "oficina-auth"
    }, local.newrelic_env)
  }

  tags = local.newrelic_habilitado ? { "NR.Apm.Lambda.Mode" = "true" } : {}

  depends_on = [aws_cloudwatch_log_group.api, aws_iam_role_policy_attachment.api_vpc]
}
