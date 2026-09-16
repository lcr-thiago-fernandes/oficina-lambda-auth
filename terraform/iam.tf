data "aws_iam_policy_document" "lambda_trust" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

# ---------- auth-api ----------
resource "aws_iam_role" "api" {
  name               = "${local.nome_api}-role"
  assume_role_policy = data.aws_iam_policy_document.lambda_trust.json
}

# Logs + ENI na VPC.
resource "aws_iam_role_policy_attachment" "api_vpc" {
  role       = aws_iam_role.api.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

data "aws_iam_policy_document" "api" {
  statement {
    sid     = "Segredos"
    actions = ["secretsmanager:GetSecretValue"]
    resources = concat(
      [data.aws_secretsmanager_secret.jwt.arn, data.aws_secretsmanager_secret.db_password.arn],
      local.newrelic_habilitado ? [data.aws_secretsmanager_secret.newrelic[0].arn] : []
    )
  }

  statement {
    sid       = "Tentativas"
    actions   = ["dynamodb:GetItem", "dynamodb:PutItem", "dynamodb:UpdateItem", "dynamodb:DeleteItem"]
    resources = [aws_dynamodb_table.tentativas.arn]
  }
}

resource "aws_iam_role_policy" "api" {
  name   = "${local.nome_api}-policy"
  role   = aws_iam_role.api.id
  policy = data.aws_iam_policy_document.api.json
}

# ---------- authorizer ----------
resource "aws_iam_role" "authorizer" {
  name               = "${local.nome_authorizer}-role"
  assume_role_policy = data.aws_iam_policy_document.lambda_trust.json
}

resource "aws_iam_role_policy_attachment" "authorizer_basic" {
  role       = aws_iam_role.authorizer.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

data "aws_iam_policy_document" "authorizer" {
  statement {
    sid     = "SegredoJwt"
    actions = ["secretsmanager:GetSecretValue"]
    resources = concat(
      [data.aws_secretsmanager_secret.jwt.arn],
      local.newrelic_habilitado ? [data.aws_secretsmanager_secret.newrelic[0].arn] : []
    )
  }
}

resource "aws_iam_role_policy" "authorizer" {
  name   = "${local.nome_authorizer}-policy"
  role   = aws_iam_role.authorizer.id
  policy = data.aws_iam_policy_document.authorizer.json
}
