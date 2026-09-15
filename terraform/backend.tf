# Mesmo bucket/tabela do bootstrap (scripts/bootstrap.sh em oficina-infra-k8s).
# Cada repositório tem a própria key: nenhum lê o state do outro.
terraform {
  backend "s3" {
    bucket         = "oficina-tfstate-fiap-15soat"
    key            = "lambda-auth/terraform.tfstate"
    region         = "us-east-1"
    dynamodb_table = "oficina-tfstate-lock"
    encrypt        = true
  }
}
