FROM node:16.17.0-bullseye-slim
ENV PORT 3000
EXPOSE 3000

RUN mkdir -p /usr/src/app
WORKDIR /usr/src/app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build # Build next framework


CMD ["npm", "start"]
