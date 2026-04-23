variable "aws_region" {
  description = "AWS region to deploy to"
  type        = string
}

variable "aws_account_id" {
  description = "ID de la cuenta de AWS"
  type        = string
}

variable "project_name" {
  description = "Name of the project"
  type        = string
}

variable "aws_account_id" {
  default = "543704476214"
}

variable "github_owner" {
  default = "gonzalezrodero"
}

variable "github_repo" {
  default = "SamaBot"
}