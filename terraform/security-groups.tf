# SG da auth-api (dentro da VPC). Sem ingress: Lambda nao recebe conexao.
resource "aws_security_group" "lambda_auth" {
  name        = "${var.project}-lambda-auth-sg"
  description = "oficina-auth-api: saida para RDS (5432) e Secrets Manager/DynamoDB via NAT (443)"
  vpc_id      = data.aws_ssm_parameter.vpc_id.value
}

resource "aws_vpc_security_group_egress_rule" "lambda_para_rds" {
  security_group_id            = aws_security_group.lambda_auth.id
  description                  = "PostgreSQL no RDS"
  ip_protocol                  = "tcp"
  from_port                    = 5432
  to_port                      = 5432
  referenced_security_group_id = data.aws_ssm_parameter.db_security_group_id.value
}

resource "aws_vpc_security_group_egress_rule" "lambda_https" {
  security_group_id = aws_security_group.lambda_auth.id
  description       = "Secrets Manager e DynamoDB pelo NAT Gateway"
  ip_protocol       = "tcp"
  from_port         = 443
  to_port           = 443
  cidr_ipv4         = "0.0.0.0/0"
}

# Regra de ENTRADA no SG do RDS (que pertence ao oficina-infra-db). Criada AQUI porque
# este SG so existe depois do infra-db rodar. CONTRATO: o infra-db precisa declarar as
# regras do SG do RDS como recursos separados (aws_vpc_security_group_*_rule), NAO
# inline no aws_security_group — inline + separado no mesmo SG fazem o Terraform de um
# repo apagar a regra do outro a cada apply.
resource "aws_vpc_security_group_ingress_rule" "rds_recebe_da_lambda" {
  security_group_id            = data.aws_ssm_parameter.db_security_group_id.value
  description                  = "PostgreSQL a partir da oficina-auth-api"
  ip_protocol                  = "tcp"
  from_port                    = 5432
  to_port                      = 5432
  referenced_security_group_id = aws_security_group.lambda_auth.id
}
