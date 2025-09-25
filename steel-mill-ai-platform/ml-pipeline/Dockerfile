FROM python:3.11-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements and install Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY models/ ./models/
COPY inference/ ./inference/
COPY data_preprocessing/ ./data_preprocessing/

# Create directories for model storage
RUN mkdir -p /app/models /app/data

# Expose port
EXPOSE 8000

# Run the inference service
CMD ["python", "-m", "uvicorn", "inference.inference_service:app", "--host", "0.0.0.0", "--port", "8000"]