# Output the exact URL to paste into Meta for Developers
output "whatsapp_webhook_url" {
  description = "Copy this URL and paste it into the Meta Webhook configuration"
  value       = "${aws_lambda_function_url.api_url.function_url}api/whatsapp/webhook"
}

output "lambda_url_raw" {
  description = "The raw base URL of the Lambda function"
  value       = aws_lambda_function_url.api_url.function_url
}