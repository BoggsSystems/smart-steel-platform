{{/*
Expand the name of the chart.
*/}}
{{- define "steel-mill-platform.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "steel-mill-platform.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "steel-mill-platform.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "steel-mill-platform.labels" -}}
helm.sh/chart: {{ include "steel-mill-platform.chart" . }}
{{ include "steel-mill-platform.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "steel-mill-platform.selectorLabels" -}}
app.kubernetes.io/name: {{ include "steel-mill-platform.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "steel-mill-platform.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "steel-mill-platform.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Generate service name
*/}}
{{- define "steel-mill-platform.serviceName" -}}
{{- printf "%s-%s" (include "steel-mill-platform.fullname" .) .name }}
{{- end }}

{{/*
Generate image name
*/}}
{{- define "steel-mill-platform.image" -}}
{{- if .Values.global.imageRegistry }}
{{- printf "%s/%s" .Values.global.imageRegistry .image }}
{{- else }}
{{- printf "%s/%s" .Values.image.registry .image }}
{{- end }}
{{- end }}

{{/*
Generate resource requirements
*/}}
{{- define "steel-mill-platform.resources" -}}
{{- if .Values.development.enabled }}
resources:
  requests:
    memory: {{ .Values.development.resources.requests.memory }}
    cpu: {{ .Values.development.resources.requests.cpu }}
  limits:
    memory: {{ .Values.development.resources.limits.memory }}
    cpu: {{ .Values.development.resources.limits.cpu }}
{{- else }}
resources:
  requests:
    memory: {{ .resources.requests.memory }}
    cpu: {{ .resources.requests.cpu }}
  limits:
    memory: {{ .resources.limits.memory }}
    cpu: {{ .resources.limits.cpu }}
{{- end }}
{{- end }}