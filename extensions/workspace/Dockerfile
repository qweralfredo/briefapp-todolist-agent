FROM node:22-alpine
WORKDIR /app

# Copy root configurations and lockfile
COPY package.json package-lock.json ./
# Copy workspace-server package.json so workspaces are properly mapped
COPY workspace-server/package.json workspace-server/

# Install all dependencies (including root dependencies like googleapis)
RUN npm install --ignore-scripts

# Copy the rest of the source code
COPY . .

# Build the project (builds all workspaces)
RUN npm run build

# Run the MCP Server
CMD ["node", "workspace-server/dist/index.js"]
