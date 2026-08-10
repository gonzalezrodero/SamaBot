# Output the exact URL to paste into Meta for Developers
output "whatsapp_webhook_url" {
  description = "Copy this URL and paste it into the Meta Webhook configuration"
  value       = "${aws_lambda_function_url.api_url.function_url}api/whatsapp/webhook"
}

output "lambda_url_raw" {
  description = "The raw base URL of the Lambda function"
  value       = aws_lambda_function_url.api_url.function_url
}

output "cognito_user_pool_id" {
  description = "The ID of the Cognito User Pool for Admin UI"
  value       = aws_cognito_user_pool.admin_pool.id
}

output "cognito_client_id" {
  description = "The ID of the Cognito App Client for Admin UI"
  value       = aws_cognito_user_pool_client.spa_client.id
}

output "cognito_hosted_ui_url" {
  description = "The URL to access the Cognito Hosted UI for login"
  value       = "https://${aws_cognito_user_pool_domain.admin_domain.domain}.auth.${var.aws_region}.amazoncognito.com/login?client_id=${aws_cognito_user_pool_client.spa_client.id}&response_type=code&scope=email+openid+profile&redirect_uri=${var.admin_ui_callback_urls[0]}"
}