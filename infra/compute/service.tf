# ECS Service to maintain the desired number of instances
resource "aws_ecs_service" "main" {
  name            = "${var.project_name}-service"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.backend.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  network_configuration {
    security_groups  = [aws_security_group.ecs_tasks.id]
    subnets          = data.terraform_remote_state.network.outputs.public_subnet_ids
    assign_public_ip = true # Required to pull images from ECR if not using PrivateLink
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.api.arn
    container_name   = "${var.project_name}-api-container"
    container_port   = 8080
  }

  # Ensure the Listener is ready before registering targets
  depends_on = [aws_lb_listener.https]
}