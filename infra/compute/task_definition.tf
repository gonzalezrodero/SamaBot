# ==============================================================================
# 1. DYNAMIC CONNECTION STRING GENERATION 
# ==============================================================================

# Fetch the original RDS secret version containing username and password
data "aws_secretsmanager_secret_version" "db_creds" {
  secret_id = data.terraform_remote_state.database.outputs.db_password_secret_arn
}

locals {
  # Parse the JSON string from Secrets Manager into a map
  db_creds = jsondecode(data.aws_secretsmanager_secret_version.db_creds.secret_string)

  # Assemble the full connection string using RDS endpoint, fixed port/db, and secret credentials
  # This avoids hardcoding sensitive data and allows dynamic host resolution
  marten_conn_string = "Host=${data.terraform_remote_state.database.outputs.db_endpoint};Port=5432;Database=samabot;Username=${local.db_creds.username};Password=${local.db_creds.password};"
}

# Create a dedicated Secret for the application connection string
# This keeps the final string encrypted and out of plain-sight environment variables
resource "aws_secretsmanager_secret" "app_connection_string" {
  name                    = "${var.project_name}/${var.app_environment}/marten-connection-string"
  description             = "Full secure connection string for SamaBot API"
  recovery_window_in_days = 0 # Allows immediate deletion/recreation for dev iterations
}

resource "aws_secretsmanager_secret_version" "app_connection_string_value" {
  secret_id     = aws_secretsmanager_secret.app_connection_string.id
  secret_string = local.marten_conn_string
}


# ==============================================================================
# 2. IAM POLICY FOR ECS TASK EXECUTION
# ==============================================================================

# Policy document granting permission to read the required secrets
data "aws_iam_policy_document" "ecs_task_execution_policy_extra" {
  statement {
    actions = [
      "secretsmanager:GetSecretValue",
      "kms:Decrypt"
    ]
    resources = [
      data.terraform_remote_state.database.outputs.db_password_secret_arn,
      aws_secretsmanager_secret.app_connection_string.arn,
      "*"
    ]
  }
}

resource "aws_iam_role_policy" "ecs_task_execution_policy_extra" {
  name   = "${var.project_name}-ecs-task-execution-policy-extra"
  role   = aws_iam_role.ecs_task_execution_role.id
  policy = data.aws_iam_policy_document.ecs_task_execution_policy_extra.json
}


# ==============================================================================
# 3. ECS TASK DEFINITION
# ==============================================================================

resource "aws_ecs_task_definition" "backend" {
  family                   = "${var.project_name}-api"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = 256
  memory                   = 512
  execution_role_arn       = aws_iam_role.ecs_task_execution_role.arn

  container_definitions = jsonencode([
    {
      name      = "${var.project_name}-api-container"
      image     = "${aws_ecr_repository.backend.repository_url}:latest"
      essential = true

      portMappings = [
        {
          containerPort = 8080
          hostPort      = 8080
          protocol      = "tcp"
        }
      ]

      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = var.app_environment }
      ]

      secrets = [
        {
          name      = "ConnectionStrings__Marten",
          valueFrom = aws_secretsmanager_secret.app_connection_string.arn
        }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = "/ecs/${var.project_name}-api"
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "ecs"
        }
      }
    }
  ])
}


# ==============================================================================
# 4. CLOUDWATCH LOG GROUP
# ==============================================================================

resource "aws_cloudwatch_log_group" "api_logs" {
  name              = "/ecs/${var.project_name}-api"
  retention_in_days = 7
}