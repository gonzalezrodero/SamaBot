resource "aws_sqs_queue" "bot_dlq" {
  name = "${var.project_name}-messages-dlq"
}

resource "aws_sqs_queue" "bot_queue" {
  name                       = "${var.project_name}-messages-queue"
  visibility_timeout_seconds = 60

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.bot_dlq.arn
    maxReceiveCount     = 3
  })
}