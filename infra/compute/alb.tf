# Security Group for ALB: Only allow HTTP/HTTPS from the internet
resource "aws_security_group" "alb_sg" {
  name        = "${var.project_name}-alb-sg"
  description = "Security group for Application Load Balancer"
  vpc_id      = data.terraform_remote_state.network.outputs.vpc_id

  ingress {
    description = "HTTP from Internet"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # Note: HTTPS ingress will be added later when we have a certificate

  egress {
    description = "Allow all outbound traffic"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "${var.project_name}-alb-sg"
  }
}

# The Application Load Balancer
resource "aws_lb" "main" {
  name               = "${var.project_name}-alb"
  internal           = false # Must be false to receive internet traffic (webhooks)
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb_sg.id]
  subnets            = data.terraform_remote_state.network.outputs.public_subnet_ids

  # Security: Drop invalid HTTP headers to prevent request smuggling
  drop_invalid_header_fields = true

  enable_deletion_protection = false

  tags = {
    Name = "${var.project_name}-alb"
  }
}