# Front Hiitop - Angular (Node 20)
FROM node:20-alpine

RUN apk add --no-cache bash

ENV NODE_ENV=development
ENV NODE_OPTIONS="--max_old_space_size=8192"

WORKDIR /app

# Angular CLI (compatible con Angular 19)
RUN npm install -g @angular/cli@19

COPY package*.json ./
RUN npm install

COPY . .
EXPOSE 4200

CMD ["ng", "serve", "--host", "0.0.0.0", "--poll", "2000"]
