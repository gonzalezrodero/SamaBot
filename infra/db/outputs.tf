output "db_endpoint" {
  description = "The connection endpoint for the database"
  value       = aws_db_instance.postgres.endpoint
}

output "db_password_secret_arn" {
  description = "The ARN of the secret managed by RDS"
  value       = aws_db_instance.postgres.master_user_secret[0].secret_arn
}