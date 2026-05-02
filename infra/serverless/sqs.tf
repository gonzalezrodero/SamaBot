resource "aws_sqs_queue" "bot_dlq" {
  name = "wolverine-dead-letter-queue"
}

resource "aws_sqs_queue" "bot_queue" {
  name                       = "${var.project_name}-messages-queue"
  visibility_timeout_seconds = 60

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.bot_dlq.arn
    maxReceiveCount     = 3
  })
}