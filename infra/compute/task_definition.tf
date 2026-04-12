# 1. IAM Policy para que ECS pueda leer el secreto de la base de datos
data "aws_iam_policy_document" "ecs_task_execution_policy_extra" {
  statement {
    actions = [
      "secretsmanager:GetSecretValue",
      "kms:Decrypt"
    ]
    resources = [
      # REFERENCIA CORREGIDA: Usamos el output del estado remoto de la DB
      data.terraform_remote_state.database.outputs.db_password_secret_arn,
      "*" 
    ]
  }
}

resource "aws_iam_role_policy" "ecs_task_execution_policy_extra" {
  name   = "${var.project_name}-ecs-task-execution-policy-extra"
  role   = aws_iam_role.ecs_task_execution_role.id
  policy = data.aws_iam_policy_document.ecs_task_execution_policy_extra.json
}

# 2. ECS Task Definition
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
        { name = "ASPNETCORE_ENVIRONMENT", value = var.app_environment },
        { name = "DB_HOST", value = data.terraform_remote_state.database.outputs.db_endpoint }
      ]

      secrets = [
        {
          name      = "DB_CREDENTIALS_JSON",
          # REFERENCIA CORREGIDA: Aquí también usamos el estado remoto
          valueFrom = data.terraform_remote_state.database.outputs.db_password_secret_arn
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

# 3. CloudWatch Log Group
resource "aws_cloudwatch_log_group" "api_logs" {
  name              = "/ecs/${var.project_name}-api"
  retention_in_days = 7
}