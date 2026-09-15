# Contrato via SSM (RFC-004): nenhum terraform_remote_state. Se um parametro nao existir,
# o plan falha aqui, com o nome do parametro na mensagem.
data "aws_ssm_parameter" "api_id" {
  name = "/${var.project}/apigw/api_id"
}

data "aws_ssm_parameter" "vpc_link_integration_id" {
  name = "/${var.project}/apigw/vpc_link_integration_id"
}

data "aws_ssm_parameter" "vpc_id" {
  name = "/${var.project}/network/vpc_id"
}

data "aws_ssm_parameter" "private_subnet_ids" {
  name = "/${var.project}/network/private_subnet_ids"
}

data "aws_ssm_parameter" "db_endpoint" {
  name = "/${var.project}/db/endpoint"
}

data "aws_ssm_parameter" "db_security_group_id" {
  name = "/${var.project}/db/security_group_id"
}

data "aws_secretsmanager_secret" "jwt" {
  name = "${var.project}/jwt_secret"
}

data "aws_secretsmanager_secret" "db_password" {
  name = "${var.project}/db_password"
}

data "aws_secretsmanager_secret" "newrelic" {
  count = local.newrelic_habilitado ? 1 : 0
  name  = var.newrelic_license_key_secret_name
}
