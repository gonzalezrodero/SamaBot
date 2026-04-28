# Fetch DB credentials
data "aws_secretsmanager_secret_version" "db_creds" {
  secret_id = data.terraform_remote_state.database.outputs.db_password_secret_arn
}

# Fetch WhatsApp Parameters from SSM
data "aws_ssm_parameter" "app_secret" {
  name = "/chatbot/dev/whatsapp/app-secret"
}
data "aws_ssm_parameter" "phone_id" {
  name = "/chatbot/dev/whatsapp/phone-id"
}
data "aws_ssm_parameter" "access_token" {
  name = "/chatbot/dev/whatsapp/access-token"
}
data "aws_ssm_parameter" "verify_token" {
  name = "/chatbot/dev/whatsapp/verify-token"
}

locals {
  db_secret_arn   = data.terraform_remote_state.database.outputs.db_password_secret_arn
  app_secrets_arn = aws_secretsmanager_secret.app_connection_string.arn
}