variable "aws_region" {
  description = "AWS region to deploy to"
  type        = string
}

variable "project_name" {
  description = "Name of the project"
  type        = string
}

variable "app_environment" {
  description = "The environment name for ASP.NET Core"
  type        = string
}