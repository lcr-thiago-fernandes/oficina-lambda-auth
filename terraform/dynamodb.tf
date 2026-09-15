# Contador de falhas de autenticacao (protecao contra forca bruta). Item:
# { chave (S, PK), falhas (N), expira_em (N, epoch s) }. TTL e faxina; a leitura
# confere expira_em em codigo (LimitadorDeTentativasDynamoDb).
resource "aws_dynamodb_table" "tentativas" {
  name         = local.nome_tabela
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "chave"

  attribute {
    name = "chave"
    type = "S"
  }

  ttl {
    attribute_name = "expira_em"
    enabled        = true
  }
}
