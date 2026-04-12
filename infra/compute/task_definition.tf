# 1. Fetch the DB Secret metadata created in the Database phase
# The RDS instance ID was defined in the previous state
data "aws_secretsmanager_secret" "db_password" {
  name = "rds!db-chatbot-db" # Matches the identifier used during DB creation
}

# 2. Grant the ECS Task Execution Role permission to decrypt the secret
# Security: This allows the Fargate agent to fetch credentials at runtime
data "aws_iam_policy_document" "ecs_task_execution_policy_extra" {
  statement {
    actions = [
      "secretsmanager:GetSecretValue",
      "kms:Decrypt"
    ]
    resources = [
      data.aws_secretsmanager_secret.db_password.arn,
      "*" # If using default AWS managed key, otherwise specify KMS ARN
    ]
  }
}

resource "aws_iam_role_policy" "ecs_task_execution_policy_extra" {
  name   = "${var.project_name}-ecs-task-execution-policy-extra"
  role   = aws_iam_role.ecs_task_execution_role.id
  policy = data.aws_iam_policy_document.ecs_task_execution_policy_extra.json
}

# 3. ECS Task Definition: The blueprint of your .NET application
resource "aws_ecs_task_definition" "backend" {
  family                   = "${var.project_name}-api"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = 256 # 0.25 vCPU - Cost-effective for MVP
  memory                   = 512 # 512 MB RAM
  execution_role_arn       = aws_iam_role.ecs_task_execution_role.arn
  
  # Note: A 'task_role_arn' would be added here if the code itself needs to access S3/DynamoDB
  # For now, only the Execution Role is needed to pull the image and secrets

  container_definitions = jsonencode([
    {
      name      = "${var.project_name}-api-container"
      image     = "${aws_ecr_repository.backend.repository_url}:latest"
      essential = true
      
      portMappings = [
        {
          containerPort = 8080 # Default .NET internal port
          hostPort      = 8080
          protocol      = "tcp"
        }
      ]

      # Non-sensitive environment variables
      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = var.app_environment },
        { name = "DB_HOST", value = data.terraform_remote_state.database.outputs.db_endpoint }      
        ]
      # Security: Injecting the RDS password JSON directly into an environment variable
      # Your .NET code will need to parse this JSON string to get the 'password' field
      secrets = [
        {
          name      = "DB_CREDENTIALS_JSON",
          valueFrom = data.aws_secretsmanager_secret.db_password.arn
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

# 4. CloudWatch Log Group for application monitoring
resource "aws_cloudwatch_log_group" "api_logs" {
  name              = "/ecs/${var.project_name}-api"
  retention_in_days = 7 # Cost management: Delete logs after one week
}