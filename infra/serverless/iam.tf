# ==============================================================================
# 1. TRUST POLICY
# ==============================================================================
data "aws_iam_policy_document" "lambda_assume_role" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "lambda_exec" {
  name               = "${var.project_name}-lambda-exec-role"
  assume_role_policy = data.aws_iam_policy_document.lambda_assume_role.json
}

# ==============================================================================
# 2. BASIC SQS AND CLOUDWATCH LOGS PERMISSIONS
# ==============================================================================
resource "aws_iam_role_policy_attachment" "lambda_basic_sqs" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaSQSQueueExecutionRole"
}

# ==============================================================================
# 3. CUSTOM PERMISSIONS
# ==============================================================================
data "aws_iam_policy_document" "lambda_custom_permissions" {

  # Allow the API (Lambda 1) to send messages to the queue
  statement {
    effect = "Allow"
    actions = [
      "sqs:SendMessage",
      "sqs:GetQueueUrl",
      "sqs:GetQueueAttributes"
    ]
    resources = [
      aws_sqs_queue.bot_queue.arn,
      aws_sqs_queue.bot_dlq.arn
    ]
  }

  # Allow reading database credentials and WhatsApp tokens
  statement {
    effect = "Allow"
    actions = [
      "secretsmanager:GetSecretValue",
      "ssm:GetParameters",
      "ssm:GetParameter",
      "ssm:GetParametersByPath",
      "kms:Decrypt"
    ]
    resources = [
      data.terraform_remote_state.database.outputs.db_password_secret_arn,
      aws_secretsmanager_secret.app_connection_string.arn,
      "arn:aws:ssm:${var.aws_region}:${var.aws_account_id}:parameter/chatbot/*"
    ]
  }
}

resource "aws_iam_role_policy" "lambda_custom" {
  name   = "${var.project_name}-lambda-custom-policy"
  role   = aws_iam_role.lambda_exec.id
  policy = data.aws_iam_policy_document.lambda_custom_permissions.json
}

# ==============================================================================
# 4. BEDROCK PERMISSIONS
# ==============================================================================
resource "aws_iam_role_policy_attachment" "lambda_bedrock" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = data.terraform_remote_state.bootstrap.outputs.bedrock_policy_arn
}