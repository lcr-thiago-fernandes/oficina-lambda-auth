output "auth_api_function_name" {
  value = aws_lambda_function.api.function_name
}

output "authorizer_function_name" {
  value = aws_lambda_function.authorizer.function_name
}

output "authorizer_id" {
  value = aws_apigatewayv2_authorizer.jwt.id
}

output "tabela_tentativas" {
  value = aws_dynamodb_table.tentativas.name
}

output "rotas" {
  value = [
    aws_apigatewayv2_route.auth_cliente.route_key,
    aws_apigatewayv2_route.auth_admin.route_key,
    aws_apigatewayv2_route.api_protegida.route_key,
  ]
}
