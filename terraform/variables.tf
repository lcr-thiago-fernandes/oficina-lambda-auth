variable "region" {
  description = "Regiao AWS."
  type        = string
  default     = "us-east-1"
}

variable "project" {
  description = "Prefixo dos recursos (mesmo dos outros repositorios)."
  type        = string
  default     = "oficina"
}

variable "api_zip_path" {
  description = "Caminho do zip publicado da oficina-auth-api (gerado por scripts/empacotar.sh)."
  type        = string
  default     = "../artefatos/auth-api.zip"
}

variable "authorizer_zip_path" {
  description = "Caminho do zip publicado do oficina-auth-authorizer."
  type        = string
  default     = "../artefatos/auth-authorizer.zip"
}

variable "api_memory_mb" {
  description = "Memoria da auth-api. CPU e proporcional a memoria; 512 mitiga cold start (design, secao 4)."
  type        = number
  default     = 512
}

variable "authorizer_memory_mb" {
  type    = number
  default = 256
}

variable "authorizer_cache_ttl_seconds" {
  description = "Cache da resposta do authorizer no API Gateway, por valor do header Authorization."
  type        = number
  default     = 300
}

variable "log_retention_days" {
  type    = number
  default = 14
}

# --- New Relic (opcional; a conta ainda nao existe) ---
variable "newrelic_layer_arn" {
  description = "ARN da layer NewRelicDotnet para a regiao (ex.: arn:aws:lambda:us-east-1:451483290750:layer:NewRelicDotnet:NN). null = sem instrumentacao."
  type        = string
  default     = null
}

variable "newrelic_account_id" {
  type    = string
  default = null
}

variable "newrelic_license_key_secret_name" {
  type    = string
  default = "oficina/newrelic_license_key"
}
