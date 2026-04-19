output "terraform_state_bucket" {
  value = aws_s3_bucket.tf_state.id
}

output "github_actions_role_arn" {
  value = aws_iam_role.github_actions.arn
}

output "ecs_task_role_arn" {
  value = aws_iam_role.ecs_task_role.arn
}

output "ecs_task_role_name" {
  value = aws_iam_role.ecs_task_role.name
}