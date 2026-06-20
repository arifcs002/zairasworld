# Build Stage
FROM node:20-alpine AS build-step
WORKDIR /app

# Copy package files and install dependencies
COPY package.json package-lock.json ./
RUN npm ci --legacy-peer-deps

# Copy source code and build project
COPY . ./
RUN npm run build -- --configuration=production

# Runtime Stage
FROM nginx:alpine
COPY --from=build-step /app/dist/admin /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
