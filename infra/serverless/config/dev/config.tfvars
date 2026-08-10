project_name    = "automatic-envelopes"
app_environment = "Development"
aws_account_id  = "543704476214"
aws_region      = "eu-west-1"

cognito_auth_domain    = "automatic-envelopes-admin-dev"
admin_ui_callback_urls = ["http://localhost:3000/callback", "https://admin-dev.tudominio.com/callback"]
admin_ui_logout_urls   = ["http://localhost:3000/logout", "https://admin-dev.tudominio.com/logout"]