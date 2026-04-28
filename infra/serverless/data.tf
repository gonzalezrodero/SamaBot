# ==============================================================================
# 1. REMOTE STATES (Cross-stack references)
# ==============================================================================
data "terraform_remote_state" "database" {
  backend = "s3"
  config = {
    bucket = "chatbot-tf-state-${var.aws_account_id}"
    key    = "db/terraform.tfstate"
    region = var.aws_region
  }
}

data "terraform_remote_state" "bootstrap" {
  backend = "s3"
  config = {
    bucket = "chatbot-tf-state-${var.aws_account_id}"
    key    = "bootstrap/terraform.tfstate"
    region = var.aws_region
  }
}

# ==============================================================================
# 2. MARTEN CONNECTION STRING SECRET ASSEMBLY
# ==============================================================================
# Fetch the raw DB credentials from the database stack
data "aws_secretsmanager_secret_version" "db_creds" {
  secret_id = data.terraform_remote_state.database.outputs.db_password_secret_arn
}

# Create a new secret for the Lambda containing the full connection string
resource "aws_secretsmanager_secret" "app_connection_string" {
  name                    = "${var.project_name}-${var.app_environment}-marten-conn"
  recovery_window_in_days = 0 
}

resource "aws_secretsmanager_secret_version" "app_connection_string_value" {
  secret_id     = aws_secretsmanager_secret.app_connection_string.id
  secret_string = "Host=${data.terraform_remote_state.database.outputs.db_endpoint};Port=5432;Database=${var.project_name};Username=${jsondecode(data.aws_secretsmanager_secret_version.db_creds.secret_string).username};Password=${jsondecode(data.aws_secretsmanager_secret_version.db_creds.secret_string).password};SSL Mode=Require;Trust Server Certificate=true;"
}

# ==============================================================================
# 3. LOCALS
# ==============================================================================
locals {
  db_secret_arn   = data.terraform_remote_state.database.outputs.db_password_secret_arn
  app_secrets_arn = aws_secretsmanager_secret.app_connection_string.arn
}