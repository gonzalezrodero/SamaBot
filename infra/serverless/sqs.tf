resource "aws_sqs_queue" "bot_queue" {
  name                       = "chatbot-messages-queue"
  visibility_timeout_seconds = 60
}