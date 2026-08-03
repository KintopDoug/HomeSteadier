import { createFileRoute } from '@tanstack/react-router'
import { Construction } from '../pages/Construction'

export const Route = createFileRoute('/_authenticated/construction')({
  component: Construction,
})
