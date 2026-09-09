# 1. Main User Pool
resource "aws_cognito_user_pool" "admin_pool" {
  name = "automatic-envelopes-admin-pool"

  password_policy {
    minimum_length    = 8
    require_lowercase = true
    require_numbers   = true
    require_symbols   = true
    require_uppercase = true
  }

  admin_create_user_config {
    allow_admin_create_user_only = true
  }
}

# 2. Free AWS Hosted UI Subdomain
resource "aws_cognito_user_pool_domain" "admin_domain" {
  domain       = var.cognito_auth_domain
  user_pool_id = aws_cognito_user_pool.admin_pool.id
}

# 3. Client App
resource "aws_cognito_user_pool_client" "spa_client" {
  name         = "automatic-envelopes-spa-client"
  user_pool_id = aws_cognito_user_pool.admin_pool.id

  generate_secret = false

  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_flows                  = ["code"]
  allowed_oauth_scopes                 = ["email", "openid", "profile"]

  callback_urls = var.admin_ui_callback_urls
  logout_urls   = var.admin_ui_logout_urls

  supported_identity_providers = ["COGNITO"]
}

# 4. Cognito Groups (RBAC)
resource "aws_cognito_user_group" "admin_group" {
  name         = "admin"
  user_pool_id = aws_cognito_user_pool.admin_pool.id
  description  = "Global Administrators with access to all tenants"
  precedence   = 1
}

resource "aws_cognito_user_group" "tenant_sama_group" {
  name         = "club-basquet-sama"
  user_pool_id = aws_cognito_user_pool.admin_pool.id
  description  = "Administrators for Club Basquet Sama"
  precedence   = 10
}