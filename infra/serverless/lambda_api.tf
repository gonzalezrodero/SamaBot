resource "aws_lambda_function" "api" {
  function_name = "${var.project_name}-api"
  role          = aws_iam_role.lambda_exec.arn
  package_type  = "Image"
  image_uri     = "${aws_ecr_repository.backend.repository_url}:${var.image_tag}"

  environment {
    variables = {
      ASPNETCORE_ENVIRONMENT   = var.app_environment
      WhatsApp__BaseUrl        = "https://graph.facebook.com/v19.0/"
      BedrockSettings__Region  = var.aws_region
      BedrockSettings__ModelId = "eu.anthropic.claude-haiku-4-5-20251001-v1:0"

      # Secure pointers (Visible in AWS Console)
      SECRET_ARN_MARTEN = aws_secretsmanager_secret.app_connection_string.arn
      SSM_PATH_WHATSAPP = "/chatbot/dev/whatsapp/"
    }
  }

  image_config {
    command = ["SamaBot.Api"]
  }
}

resource "aws_lambda_function_url" "api_url" {
  function_name      = aws_lambda_function.api.function_name
  authorization_type = "NONE"
}