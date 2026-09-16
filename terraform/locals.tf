data "aws_caller_identity" "atual" {}

locals {
  nome_api        = "${var.project}-auth-api"
  nome_authorizer = "${var.project}-auth-authorizer"
  nome_tabela     = "${var.project}-auth-tentativas"

  # /oficina/network/private_subnet_ids e um StringList ("subnet-a,subnet-b").
  private_subnet_ids = split(",", data.aws_ssm_parameter.private_subnet_ids.value)

  api_execution_arn = "arn:aws:execute-api:${var.region}:${data.aws_caller_identity.atual.account_id}:${data.aws_ssm_parameter.api_id.value}"

  newrelic_habilitado = var.newrelic_layer_arn != null

  # Variaveis do agente .NET em Lambda (docs New Relic, "env-variables-lambda"); a
  # license key e lida pela extensao direto do Secrets Manager, nao fica em claro.
  newrelic_env = local.newrelic_habilitado ? {
    CORECLR_ENABLE_PROFILING               = "1"
    CORECLR_PROFILER                       = "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
    CORECLR_NEWRELIC_HOME                  = "/opt/lib/newrelic-dotnet-agent"
    CORECLR_PROFILER_PATH                  = "/opt/lib/newrelic-dotnet-agent/libNewRelicProfiler.so"
    NEW_RELIC_ACCOUNT_ID                   = coalesce(var.newrelic_account_id, "")
    NEW_RELIC_APM_LAMBDA_MODE              = "true"
    NEW_RELIC_LICENSE_KEY_SECRET           = var.newrelic_license_key_secret_name
    NEW_RELIC_LAMBDA_EXTENSION_ENABLED     = "true"
    NEW_RELIC_EXTENSION_SEND_FUNCTION_LOGS = "true"
  } : {}

  tags = {
    Project   = var.project
    ManagedBy = "Terraform"
    Repo      = "oficina-lambda-auth"
    Fase      = "fase-3"
  }
}
